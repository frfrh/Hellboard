// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.Platform;

public sealed class PixelRectTests
{
    [Fact]
    public void Intersection_of_overlapping_rectangles()
    {
        var a = new PixelRect(-100, -50, 200, 100);
        var b = new PixelRect(0, 0, 300, 300);

        a.Intersect(b).Should().Be(new PixelRect(0, 0, 100, 50));
    }

    [Fact]
    public void Touching_rectangles_do_not_intersect()
    {
        var a = new PixelRect(-1920, 0, 1920, 1080);
        var b = new PixelRect(0, 0, 2560, 1440);

        a.Intersect(b).IsEmpty.Should().BeTrue();
        a.Intersect(b).Area.Should().Be(0);
    }

    [Fact]
    public void Area_of_a_4K_rectangle_does_not_overflow()
    {
        new PixelRect(0, 0, 7680, 4320).Area.Should().Be(33_177_600);
    }
}
