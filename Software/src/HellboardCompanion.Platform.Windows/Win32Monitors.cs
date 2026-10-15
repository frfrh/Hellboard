// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using HellboardCompanion.Core.Platform;
using static HellboardCompanion.Platform.Windows.Interop.NativeMethods;

namespace HellboardCompanion.Platform.Windows;

/// <summary>Enumerates monitors in physical pixels (the process is per-monitor DPI aware v2).</summary>
internal static class Win32Monitors
{
    public static IReadOnlyList<(nint Handle, MonitorInfo Info)> GetAll()
    {
        var monitors = new List<(nint, MonitorInfo)>();
        MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            var info = TryGetInfo(hMonitor);
            if (info is not null)
            {
                monitors.Add((hMonitor, info));
            }

            return true;
        };

        EnumDisplayMonitors(0, 0, Marshal.GetFunctionPointerForDelegate(callback), 0);
        GC.KeepAlive(callback);
        return monitors;
    }

    private static unsafe MonitorInfo? TryGetInfo(nint hMonitor)
    {
        var info = new MONITORINFOEXW { CbSize = (uint)sizeof(MONITORINFOEXW) };
        if (!GetMonitorInfo(hMonitor, ref info))
        {
            return null;
        }

        var deviceName = new string(info.SzDevice);
        var bounds = PixelRect.FromEdges(info.RcMonitor.Left, info.RcMonitor.Top, info.RcMonitor.Right, info.RcMonitor.Bottom);
        var scale = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;

        return new MonitorInfo(deviceName, bounds, (info.DwFlags & MONITORINFOF_PRIMARY) != 0, scale);
    }
}
