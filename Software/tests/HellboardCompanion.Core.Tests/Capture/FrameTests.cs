// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.Capture;

public sealed class FrameTests
{
    private static readonly PixelRect Bounds = new(-1920, -101, 320, 180);

    [Fact]
    public void Black_image_is_detected()
    {
        var frame = new Frame(Bounds, new byte[Bounds.Area * 4], DateTimeOffset.Now);

        frame.IsBlack().Should().BeTrue();
    }

    [Fact]
    public void Dark_game_scene_is_not_black()
    {
        var pixels = new byte[Bounds.Area * 4];
        Array.Fill(pixels, (byte)30);   // like the loadout screen background

        new Frame(Bounds, pixels, DateTimeOffset.Now).IsBlack().Should().BeFalse();
    }

    [Fact]
    public void Buffer_size_must_match_the_bounds()
    {
        var create = () => new Frame(Bounds, new byte[100], DateTimeOffset.Now);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Stride_is_four_bytes_per_pixel()
    {
        new Frame(Bounds, new byte[Bounds.Area * 4], DateTimeOffset.Now).Stride.Should().Be(1280);
    }
}
