// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>
/// Platform-independent decisions of the game locator, kept separate so they can be unit tested.
/// </summary>
public static class GameLocationRules
{
    /// <summary>
    /// Picks the monitor showing the window: the override if it exists, otherwise the monitor with
    /// the largest intersection, otherwise the nearest one.
    /// </summary>
    public static MonitorInfo SelectMonitor(PixelRect window, IReadOnlyList<MonitorInfo> monitors, string? monitorOverride = null)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor is required.", nameof(monitors));
        }

        if (monitorOverride is not null)
        {
            var forced = monitors.FirstOrDefault(m => string.Equals(m.DeviceName, monitorOverride, StringComparison.OrdinalIgnoreCase));
            if (forced is not null)
            {
                return forced;
            }
        }

        var best = monitors.MaxBy(m => m.Bounds.Intersect(window).Area)!;
        return best.Bounds.Intersect(window).Area > 0
            ? best
            : monitors.MinBy(m => m.Bounds.CenterDistanceSquared(window))!;
    }

    /// <summary>
    /// A client area that covers the whole monitor is fullscreen or a borderless window; anything
    /// smaller is a regular window.
    /// </summary>
    public static WindowMode ClassifyWindowMode(PixelRect clientBounds, PixelRect monitorBounds) =>
        clientBounds.Intersect(monitorBounds) == monitorBounds
            ? WindowMode.FullscreenOrBorderless
            : WindowMode.Windowed;
}
