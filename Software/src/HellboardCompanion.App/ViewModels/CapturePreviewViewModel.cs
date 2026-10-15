// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.App.ViewModels;

/// <summary>
/// Live preview of the captured game image (2 frames per second while the window is open) and
/// snapshot saving (implementation plan §6.7, debug view).
/// </summary>
public sealed partial class CapturePreviewViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(500);

    private readonly GameFrameGrabber? _grabber;
    private readonly DispatcherTimer? _timer;
    private Frame? _lastFrame;
    private bool _captureRunning;

    [ObservableProperty]
    private Bitmap? _preview;

    [ObservableProperty]
    private string _captureInfo = "No image";

    [ObservableProperty]
    private bool _isBlack;

    [ObservableProperty]
    private string? _lastSnapshot;

    public CapturePreviewViewModel(GameFrameGrabber? grabber, IPlatformPaths paths)
    {
        _grabber = grabber;
        SnapshotDirectory = Path.Combine(paths.DataDirectory, "snapshots");

        if (_grabber is not null)
        {
            _timer = new DispatcherTimer(PreviewInterval, DispatcherPriority.Background, (_, _) => CaptureNext());
            _timer.Start();
        }
    }

    public string SnapshotDirectory { get; }

    public void Dispose()
    {
        _timer?.Stop();
        Preview?.Dispose();
    }

    [RelayCommand]
    private void SaveSnapshot()
    {
        if (_lastFrame is null)
        {
            return;
        }

        Directory.CreateDirectory(SnapshotDirectory);
        var path = Path.Combine(SnapshotDirectory, $"snapshot-{_lastFrame.Timestamp:yyyyMMdd-HHmmss-fff}-{_lastFrame.Width}x{_lastFrame.Height}.png");
        using var bitmap = ToBitmap(_lastFrame);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
        LastSnapshot = path;
    }

    private void CaptureNext()
    {
        if (_captureRunning)
        {
            return;
        }

        _captureRunning = true;
        Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            var frame = _grabber!.Capture();
            return (frame, stopwatch.Elapsed);
        }).ContinueWith(
            task =>
            {
                _captureRunning = false;
                if (task.IsFaulted)
                {
                    CaptureInfo = $"Capture failed: {task.Exception!.GetBaseException().Message}";
                    return;
                }

                var (frame, duration) = task.Result;
                if (frame is null)
                {
                    CaptureInfo = "No image (game not running or minimized)";
                    return;
                }

                _lastFrame = frame;
                IsBlack = frame.IsBlack();
                CaptureInfo = $"{frame.Width}×{frame.Height}, captured in {duration.TotalMilliseconds:F0} ms";

                var old = Preview;
                Preview = ToBitmap(frame);
                old?.Dispose();
            },
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static WriteableBitmap ToBitmap(Frame frame)
    {
        var bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var buffer = bitmap.Lock();
        for (var y = 0; y < frame.Height; y++)
        {
            Marshal.Copy(frame.Bgra, y * frame.Stride, buffer.Address + (y * buffer.RowBytes), frame.Stride);
        }

        return bitmap;
    }
}
