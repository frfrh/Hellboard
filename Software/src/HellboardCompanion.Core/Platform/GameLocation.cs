// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

public enum GameStatus
{
    /// <summary>The game process is not running.</summary>
    NotRunning,

    /// <summary>The process runs, but its main window does not exist (yet).</summary>
    Starting,

    /// <summary>The game window exists; <see cref="GameSnapshot.Location"/> is set.</summary>
    Running,
}

public enum WindowMode
{
    /// <summary>The client area covers the whole monitor (borderless window or fullscreen).</summary>
    FullscreenOrBorderless,

    Windowed,
}

/// <summary>Where the game is shown (implementation plan §6.1).</summary>
/// <param name="ProcessId">Game process id.</param>
/// <param name="WindowHandle">Opaque platform window handle; only interpreted by platform code.</param>
/// <param name="Monitor">Monitor that shows the game.</param>
/// <param name="ClientBounds">Game client area in physical screen pixels.</param>
/// <param name="Mode">Window mode derived from client area and monitor.</param>
/// <param name="IsForeground">Whether the game window is the foreground window.</param>
/// <param name="IsMinimized">Whether the game window is minimized.</param>
public sealed record GameLocation(
    int ProcessId,
    nint WindowHandle,
    MonitorInfo Monitor,
    PixelRect ClientBounds,
    WindowMode Mode,
    bool IsForeground,
    bool IsMinimized);

/// <summary>Current state of the game as seen by <see cref="IGameLocator"/>.</summary>
public sealed record GameSnapshot(GameStatus Status, GameLocation? Location)
{
    public static GameSnapshot NotRunning { get; } = new(GameStatus.NotRunning, null);

    public static GameSnapshot Starting { get; } = new(GameStatus.Starting, null);

    public static GameSnapshot Running(GameLocation location) => new(GameStatus.Running, location);
}
