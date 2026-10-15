// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.TestData;

/// <summary>Game locator that reports a fixed state.</summary>
public sealed class FakeGameLocator(GameSnapshot snapshot) : IGameLocator
{
    public GameSnapshot Current => snapshot;

    public event EventHandler<GameSnapshot>? Changed
    {
        add { }
        remove { }
    }

    /// <summary>Game running full screen on a primary monitor of the given size.</summary>
    public static FakeGameLocator Running(int width, int height)
    {
        var bounds = new PixelRect(0, 0, width, height);
        var monitor = new MonitorInfo(@"\\.\DISPLAY1", bounds, true, 1.0);
        return new FakeGameLocator(GameSnapshot.Running(new GameLocation(1, 1, monitor, bounds, WindowMode.FullscreenOrBorderless, true, false)));
    }

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>Frame source that returns a fixed image (e.g. a local test scene) for every capture.</summary>
public sealed class FakeFrameSource(Frame? frame) : IFrameSource
{
    public List<(MonitorInfo Monitor, PixelRect Region)> Requests { get; } = [];

    public Frame? Capture(MonitorInfo monitor, PixelRect region, TimeSpan timeout)
    {
        Requests.Add((monitor, region));
        return frame ?? new Frame(region, new byte[region.Area * Frame.BytesPerPixel], DateTimeOffset.Now);
    }

    public void Dispose()
    {
    }
}
