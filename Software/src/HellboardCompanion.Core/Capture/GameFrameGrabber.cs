// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Capture;

/// <summary>
/// Captures the game's client area on the monitor reported by the <see cref="IGameLocator"/>.
/// </summary>
public sealed class GameFrameGrabber(IGameLocator gameLocator, IFrameSource frameSource)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Current state of the game.</summary>
    public GameSnapshot Game => gameLocator.Current;

    /// <summary>
    /// Returns the current image of the game, or <c>null</c> if the game is not running, minimized
    /// or no image is available.
    /// </summary>
    public Frame? Capture(TimeSpan? timeout = null)
    {
        var location = gameLocator.Current.Location;
        if (location is null || location.IsMinimized || location.ClientBounds.IsEmpty)
        {
            return null;
        }

        return frameSource.Capture(location.Monitor, location.ClientBounds, timeout ?? DefaultTimeout);
    }
}
