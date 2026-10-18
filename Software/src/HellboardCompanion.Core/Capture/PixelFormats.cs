// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Capture;

/// <summary>
/// Conversions of desktop images to the 8-bit BGRA of <see cref="Frame"/>, for screens Windows
/// delivers in another format (10 bits per channel, or HDR).
/// </summary>
public static class PixelFormats
{
    /// <summary>R10G10B10A2_UNORM (red in the lowest bits) to BGRA, alpha opaque.</summary>
    public static void FromR10G10B10A2(ReadOnlySpan<uint> source, Span<byte> bgra)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var value = source[i];
            bgra[(i * 4) + 0] = To8Bit((value >> 20) & 0x3FF);
            bgra[(i * 4) + 1] = To8Bit((value >> 10) & 0x3FF);
            bgra[(i * 4) + 2] = To8Bit(value & 0x3FF);
            bgra[(i * 4) + 3] = 255;
        }
    }

    /// <summary>
    /// R16G16B16A16_FLOAT in linear scRGB (HDR desktop) to sRGB-encoded BGRA. <paramref name="sdrWhite"/> is the
    /// scRGB value of SDR white on that monitor ("SDR content brightness", 1.0 = 80 nits, typically 2–3): the
    /// game's SDR colors come out as on an SDR screen (seen on a Windows 10 PC: without it the slot gray 47
    /// became 85 and the loadout screen was not detected). Brighter values are clipped.
    /// </summary>
    public static void FromScRgbHalf(ReadOnlySpan<Half> rgba, Span<byte> bgra, float sdrWhite = 1)
    {
        var scale = sdrWhite > 0 ? 1 / sdrWhite : 1;
        for (var i = 0; i < rgba.Length / 4; i++)
        {
            bgra[(i * 4) + 0] = ToSrgb((float)rgba[(i * 4) + 2] * scale);
            bgra[(i * 4) + 1] = ToSrgb((float)rgba[(i * 4) + 1] * scale);
            bgra[(i * 4) + 2] = ToSrgb((float)rgba[i * 4] * scale);
            bgra[(i * 4) + 3] = 255;
        }
    }

    private static byte To8Bit(uint value10) => (byte)(((value10 * 255) + 511) / 1023);

    private static byte ToSrgb(float linear)
    {
        var c = float.IsNaN(linear) ? 0 : Math.Clamp(linear, 0, 1);
        var encoded = c <= 0.0031308f ? 12.92f * c : (1.055f * MathF.Pow(c, 1 / 2.4f)) - 0.055f;
        return (byte)Math.Round(encoded * 255);
    }
}
