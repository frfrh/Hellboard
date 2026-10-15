// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Capture;

namespace HellboardCompanion.Core.Platform;

/// <summary>
/// Captures screen content (implementation plan §6.2). Pull-based: the caller decides the rate.
/// Implementations must be thread-safe.
/// </summary>
public interface IFrameSource : IDisposable
{
    /// <summary>
    /// Captures <paramref name="region"/> (physical screen pixels) of <paramref name="monitor"/>.
    /// The region is clipped to the monitor. Returns <c>null</c> if no image is available within
    /// <paramref name="timeout"/>, e.g. while the display mode changes.
    /// </summary>
    Frame? Capture(MonitorInfo monitor, PixelRect region, TimeSpan timeout);
}
