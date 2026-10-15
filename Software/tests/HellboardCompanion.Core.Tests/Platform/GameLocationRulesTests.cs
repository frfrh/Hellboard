// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.Platform;

public sealed class GameLocationRulesTests
{
    // Layout of the development machine: 2560×1440 primary at 125 %, 1920×1080 left of and slightly above it.
    private static readonly MonitorInfo Primary = new(@"\\.\DISPLAY1", new PixelRect(0, 0, 2560, 1440), true, 1.25);
    private static readonly MonitorInfo Left = new(@"\\.\DISPLAY2", new PixelRect(-1920, -101, 1920, 1080), false, 1.0);
    private static readonly MonitorInfo[] Monitors = [Primary, Left];

    [Fact]
    public void Fullscreen_game_on_primary_monitor_is_found()
    {
        GameLocationRules.SelectMonitor(Primary.Bounds, Monitors).Should().Be(Primary);
    }

    [Fact]
    public void Monitor_with_negative_coordinates_is_found()
    {
        GameLocationRules.SelectMonitor(Left.Bounds, Monitors).Should().Be(Left);
    }

    [Fact]
    public void Window_spanning_two_monitors_belongs_to_the_one_with_the_larger_part()
    {
        var mostlyLeft = new PixelRect(-1500, 100, 1600, 900);   // 1500 px on the left monitor, 100 px on the primary

        GameLocationRules.SelectMonitor(mostlyLeft, Monitors).Should().Be(Left);
    }

    [Fact]
    public void Window_outside_all_monitors_belongs_to_the_nearest_monitor()
    {
        var belowLeft = new PixelRect(-1800, 2000, 800, 600);

        GameLocationRules.SelectMonitor(belowLeft, Monitors).Should().Be(Left);
    }

    [Fact]
    public void Override_wins_over_the_detected_monitor()
    {
        GameLocationRules.SelectMonitor(Primary.Bounds, Monitors, @"\\.\display2").Should().Be(Left);
    }

    [Fact]
    public void Unknown_override_is_ignored()
    {
        GameLocationRules.SelectMonitor(Primary.Bounds, Monitors, @"\\.\DISPLAY9").Should().Be(Primary);
    }

    [Fact]
    public void Client_area_covering_the_monitor_is_fullscreen_or_borderless()
    {
        GameLocationRules.ClassifyWindowMode(Left.Bounds, Left.Bounds).Should().Be(WindowMode.FullscreenOrBorderless);
    }

    [Fact]
    public void Smaller_client_area_is_windowed()
    {
        var window = new PixelRect(100, 100, 1920, 1200);

        GameLocationRules.ClassifyWindowMode(window, Primary.Bounds).Should().Be(WindowMode.Windowed);
    }
}
