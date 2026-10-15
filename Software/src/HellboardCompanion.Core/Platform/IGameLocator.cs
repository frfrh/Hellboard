// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>
/// Finds the game window and the monitor it is shown on, and reports every change
/// (start/exit, window moved to another monitor, resolution or window mode changed, focus).
/// </summary>
public interface IGameLocator : IDisposable
{
    GameSnapshot Current { get; }

    /// <summary>Raised on a background thread whenever <see cref="Current"/> changes.</summary>
    event EventHandler<GameSnapshot>? Changed;

    void Start();
}

public sealed class GameLocatorOptions
{
    /// <summary>Process name without extension.</summary>
    public string ProcessName { get; set; } = "helldivers2";

    /// <summary>Forces a monitor by device name (e.g. <c>\\.\DISPLAY2</c>) if the automatic detection fails.</summary>
    public string? MonitorOverride { get; set; }
}
