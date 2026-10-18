// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Capture;

namespace HellboardCompanion.Core.Tests.Capture;

public sealed class PixelFormatsTests
{
    [Fact]
    public void A_10_bit_pixel_becomes_8_bit_BGRA()
    {
        // Red 1023, green 512, blue 0 (red in the lowest bits), alpha 3.
        uint pixel = 1023 | (512u << 10) | (0u << 20) | (3u << 30);
        var bgra = new byte[4];

        PixelFormats.FromR10G10B10A2([pixel], bgra);

        bgra.Should().Equal(0, 128, 255, 255);
    }

    [Theory]
    [InlineData(1f, 47)]      // SDR screen: scRGB 1.0 is white
    [InlineData(2.5f, 47)]    // HDR screen with SDR content at 200 nits
    public void The_slot_gray_of_the_game_stays_47_on_an_HDR_desktop(float sdrWhite, byte expected)
    {
        // The game draws its slot background as sRGB 47; Windows stores it as linear scRGB scaled by the SDR white level.
        var linear = MathF.Pow(((47 / 255f) + 0.055f) / 1.055f, 2.4f) * sdrWhite;
        Half[] rgba = [(Half)linear, (Half)linear, (Half)linear, (Half)1];
        var bgra = new byte[4];

        PixelFormats.FromScRgbHalf(rgba, bgra, sdrWhite);

        bgra[..3].Should().AllSatisfy(c => c.Should().BeInRange((byte)(expected - 1), (byte)(expected + 1)));
        bgra[3].Should().Be(255);
    }

    [Fact]
    public void Brighter_than_SDR_white_is_clipped()
    {
        Half[] rgba = [(Half)6f, (Half)0f, (Half)(-0.5f), (Half)1];
        var bgra = new byte[4];

        PixelFormats.FromScRgbHalf(rgba, bgra, 2.5f);

        bgra.Should().Equal(0, 0, 255, 255);
    }
}
