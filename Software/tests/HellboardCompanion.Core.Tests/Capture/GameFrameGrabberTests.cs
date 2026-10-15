// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Tests.TestData;

namespace HellboardCompanion.Core.Tests.Capture;

public sealed class GameFrameGrabberTests
{
    private static readonly MonitorInfo Monitor = new(@"\\.\DISPLAY2", new PixelRect(-1920, -101, 1920, 1080), false, 1.0);

    [Fact]
    public void Captures_the_client_area_on_the_game_monitor()
    {
        var client = new PixelRect(-1800, 0, 1280, 720);
        var source = new FakeFrameSource(null);
        var grabber = new GameFrameGrabber(new FakeGameLocator(Running(client)), source);

        grabber.Capture().Should().NotBeNull();

        source.Requests.Should().ContainSingle().Which.Should().Be((Monitor, client));
    }

    [Fact]
    public void Nothing_is_captured_when_the_game_is_not_running()
    {
        var source = new FakeFrameSource(null);
        var grabber = new GameFrameGrabber(new FakeGameLocator(GameSnapshot.NotRunning), source);

        grabber.Capture().Should().BeNull();
        source.Requests.Should().BeEmpty();
    }

    [Fact]
    public void Nothing_is_captured_when_the_game_is_minimized()
    {
        var snapshot = Running(new PixelRect(-1800, 0, 1280, 720));
        var minimized = snapshot with { Location = snapshot.Location! with { IsMinimized = true } };
        var source = new FakeFrameSource(null);
        var grabber = new GameFrameGrabber(new FakeGameLocator(minimized), source);

        grabber.Capture().Should().BeNull();
        source.Requests.Should().BeEmpty();
    }

    private static GameSnapshot Running(PixelRect client) =>
        GameSnapshot.Running(new GameLocation(1, 1, Monitor, client, WindowMode.Windowed, true, false));
}
