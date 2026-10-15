// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static HellboardCompanion.Platform.Windows.Interop.NativeMethods;

namespace HellboardCompanion.Platform.Windows;

/// <summary>
/// Captures a monitor with DXGI Desktop Duplication (implementation plan §6.2): no capture border,
/// works for borderless/fullscreen games, the GPU image is copied to a staging texture and only the
/// requested region is read back. The duplication of one monitor is kept open between calls and
/// recreated when the monitor changes or the access is lost (mode switch, secure desktop).
/// </summary>
public sealed class DesktopDuplicationFrameSource(ILogger<DesktopDuplicationFrameSource> logger) : IFrameSource
{
    private readonly Lock _lock = new();
    private Session? _session;

    public Frame? Capture(MonitorInfo monitor, PixelRect region, TimeSpan timeout)
    {
        var clipped = region.Intersect(monitor.Bounds);
        if (clipped.IsEmpty)
        {
            return null;
        }

        lock (_lock)
        {
            var previousDpiContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            try
            {
                return CaptureLocked(monitor, clipped, timeout);
            }
            finally
            {
                SetThreadDpiAwarenessContext(previousDpiContext);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _session?.Dispose();
            _session = null;
        }
    }

    private Frame? CaptureLocked(MonitorInfo monitor, PixelRect region, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (_session is null || _session.DeviceName != monitor.DeviceName)
            {
                _session?.Dispose();
                _session = Session.TryCreate(monitor, logger);
                if (_session is null)
                {
                    return null;
                }
            }

            var remaining = deadline - DateTime.UtcNow;
            var outcome = _session.TryAcquire((uint)Math.Max(remaining.TotalMilliseconds, 0));

            if (outcome == AcquireOutcome.AccessLost)
            {
                logger.LogInformation("Desktop duplication of {Monitor} lost, recreating", monitor.DeviceName);
                _session.Dispose();
                _session = null;
                if (DateTime.UtcNow >= deadline)
                {
                    return null;
                }

                continue;
            }

            // On a timeout the screen did not change; the staging texture still holds the last image.
            var local = region with { X = region.X - monitor.Bounds.X, Y = region.Y - monitor.Bounds.Y };
            return _session.HasImage ? _session.Read(local, region) : null;
        }
    }

    private enum AcquireOutcome
    {
        NewImage,
        Timeout,
        AccessLost,
    }

    /// <summary>Duplication of one monitor with its device and staging texture.</summary>
    private sealed class Session : IDisposable
    {
        private readonly ID3D11Device _device;
        private readonly IDXGIOutputDuplication _duplication;
        private ID3D11Texture2D? _staging;

        private Session(string deviceName, ID3D11Device device, IDXGIOutputDuplication duplication)
        {
            DeviceName = deviceName;
            _device = device;
            _duplication = duplication;
        }

        public string DeviceName { get; }

        public bool HasImage { get; private set; }

        public static Session? TryCreate(MonitorInfo monitor, ILogger logger)
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
            {
                using (adapter)
                {
                    for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
                    {
                        using (output)
                        {
                            if (output.Description.DeviceName.TrimEnd('\0') != monitor.DeviceName)
                            {
                                continue;
                            }

                            // The device must be created on the adapter that owns the output.
                            if (D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport, [FeatureLevel.Level_11_0], out ID3D11Device? device).Failure
                                || device is null)
                            {
                                logger.LogWarning("Could not create a Direct3D 11 device for {Monitor}", monitor.DeviceName);
                                return null;
                            }

                            try
                            {
                                using var output1 = output.QueryInterface<IDXGIOutput1>();
                                var duplication = output1.DuplicateOutput(device);
                                logger.LogInformation("Desktop duplication started for {Monitor} on {Adapter}", monitor.DeviceName, adapter.Description1.Description);
                                return new Session(monitor.DeviceName, device, duplication);
                            }
                            catch (Exception ex)
                            {
                                device.Dispose();
                                logger.LogWarning(ex, "Desktop duplication of {Monitor} is not available", monitor.DeviceName);
                                return null;
                            }
                        }
                    }
                }
            }

            logger.LogWarning("Monitor {Monitor} not found among the DXGI outputs", monitor.DeviceName);
            return null;
        }

        public AcquireOutcome TryAcquire(uint timeoutMs)
        {
            var result = _duplication.AcquireNextFrame(timeoutMs, out _, out var resource);
            if (result == Vortice.DXGI.ResultCode.WaitTimeout)
            {
                return AcquireOutcome.Timeout;
            }

            if (result.Failure)
            {
                return AcquireOutcome.AccessLost;
            }

            try
            {
                using (resource)
                using (var texture = resource!.QueryInterface<ID3D11Texture2D>())
                {
                    _staging ??= CreateStaging(texture.Description);
                    _device.ImmediateContext.CopyResource(_staging, texture);
                    HasImage = true;
                }
            }
            finally
            {
                _duplication.ReleaseFrame();
            }

            return AcquireOutcome.NewImage;
        }

        /// <summary>Reads <paramref name="local"/> (monitor-relative pixels) from the staging texture.</summary>
        public unsafe Frame Read(PixelRect local, PixelRect screen)
        {
            var context = _device.ImmediateContext;
            var mapped = context.Map(_staging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                var rowBytes = local.Width * Frame.BytesPerPixel;
                var pixels = new byte[local.Height * rowBytes];
                var source = (byte*)mapped.DataPointer + (local.Y * mapped.RowPitch) + (local.X * Frame.BytesPerPixel);
                for (var y = 0; y < local.Height; y++)
                {
                    new ReadOnlySpan<byte>(source + (y * mapped.RowPitch), rowBytes).CopyTo(pixels.AsSpan(y * rowBytes));
                }

                return new Frame(screen, pixels, DateTimeOffset.Now);
            }
            finally
            {
                context.Unmap(_staging!, 0);
            }
        }

        public void Dispose()
        {
            _staging?.Dispose();
            _duplication.Dispose();
            _device.Dispose();
        }

        private ID3D11Texture2D CreateStaging(Texture2DDescription description)
        {
            description.CPUAccessFlags = CpuAccessFlags.Read;
            description.Usage = ResourceUsage.Staging;
            description.BindFlags = BindFlags.None;
            description.MiscFlags = ResourceOptionFlags.None;
            return _device.CreateTexture2D(description);
        }
    }
}
