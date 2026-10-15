// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <param name="DeviceName">Platform device name, e.g. <c>\\.\DISPLAY2</c> on Windows.</param>
/// <param name="Bounds">Monitor area in physical pixels.</param>
/// <param name="IsPrimary">Whether this is the primary monitor.</param>
/// <param name="Scale">Display scaling factor, e.g. 1.25 for 125 %.</param>
public sealed record MonitorInfo(string DeviceName, PixelRect Bounds, bool IsPrimary, double Scale)
{
    public override string ToString() =>
        $"{DeviceName} {Bounds.Width}×{Bounds.Height} @ {Scale:P0}{(IsPrimary ? " (primary)" : string.Empty)}";
}
