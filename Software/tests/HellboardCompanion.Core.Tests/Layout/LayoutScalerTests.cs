// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.Layout;

/// <summary>
/// Expected values come from measurements on real captures (implementation plan §6.3):
/// player slot 1 interior at 2560×1440 is x 80, y 1123, 93–94 px; at 1920×1200 x 60, y 842, 70 px.
/// </summary>
public sealed class LayoutScalerTests
{
    private static readonly LoadoutLayout Layout = LoadoutLayout.Default;

    [Fact]
    public void Default_layout_is_embedded_and_complete()
    {
        Layout.ReferenceWidth.Should().Be(1920);
        Layout.ReferenceHeight.Should().Be(1080);
        Layout.PlayerSlots.Should().HaveCount(4);
        Layout.FixedMissionSlots.Should().HaveCount(3);
        Layout.OptionalMissionSlots.Should().HaveCount(4);
    }

    [Fact]
    public void Native_1440p_capture_matches_the_measured_positions()
    {
        var scaler = new LayoutScaler(Layout, new PixelRect(0, 0, 2560, 1440));

        var slot1 = scaler.ToScreen(Layout.PlayerSlots[0]);
        slot1.X.Should().Be(80);
        slot1.Y.Should().Be(1123);
        slot1.Width.Should().BeInRange(93, 95);

        var button = scaler.ToScreen(Layout.ReadyButton);
        button.Should().Be(new PixelRect(73, 1236, 560, 56));
    }

    [Fact]
    public void Sixteen_by_ten_is_scaled_by_width_and_anchored_top_left()
    {
        var scaler = new LayoutScaler(Layout, new PixelRect(0, 0, 1920, 1200));

        scaler.Scale.Should().Be(1.0);
        scaler.ToScreen(Layout.PlayerSlots[3]).Should().Be(new PixelRect(315, 842, 70, 71));
    }

    [Theory]
    [InlineData(1280, 720, 0.6667)]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(2560, 1440, 1.3333)]
    [InlineData(3840, 2160, 2.0)]
    public void Scale_follows_the_resolution(int width, int height, double expected)
    {
        new LayoutScaler(Layout, new PixelRect(0, 0, width, height)).Scale.Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void Client_position_on_a_secondary_monitor_is_respected()
    {
        var scaler = new LayoutScaler(Layout, new PixelRect(-1920, -101, 1920, 1080));

        scaler.ToScreen(Layout.ReadyButton).Should().Be(new PixelRect(-1866, 826, 421, 42));
    }

    [Fact]
    public void Slots_are_ordered_left_to_right_without_overlap()
    {
        foreach (var row in new[] { Layout.PlayerSlots, Layout.FixedMissionSlots.Concat(Layout.OptionalMissionSlots).ToList() })
        {
            for (var i = 1; i < row.Count; i++)
            {
                row[i].X.Should().BeGreaterThan(row[i - 1].X + row[i - 1].Width);
            }
        }
    }
}
