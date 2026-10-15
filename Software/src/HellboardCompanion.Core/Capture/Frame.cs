// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Capture;

/// <summary>
/// A captured image in 32-bit BGRA, tightly packed (stride = <see cref="Width"/> × 4).
/// </summary>
public sealed class Frame
{
    public const int BytesPerPixel = 4;

    public Frame(PixelRect screenBounds, byte[] bgra, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (screenBounds.IsEmpty || bgra.Length != screenBounds.Area * BytesPerPixel)
        {
            throw new ArgumentException("Pixel buffer does not match the frame size.", nameof(bgra));
        }

        ScreenBounds = screenBounds;
        Bgra = bgra;
        Timestamp = timestamp;
    }

    /// <summary>Captured area in physical screen pixels.</summary>
    public PixelRect ScreenBounds { get; }

    public int Width => ScreenBounds.Width;

    public int Height => ScreenBounds.Height;

    public int Stride => Width * BytesPerPixel;

    public byte[] Bgra { get; }

    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Share (0…1) of pixels in <paramref name="area"/> (frame-relative, clipped to the frame) for
    /// which <paramref name="predicate"/>(blue, green, red) is true.
    /// </summary>
    public double Share(PixelRect area, Func<byte, byte, byte, bool> predicate)
    {
        var clipped = area.Intersect(new PixelRect(0, 0, Width, Height));
        if (clipped.IsEmpty)
        {
            return 0;
        }

        long hits = 0;
        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            var index = (y * Stride) + (clipped.X * BytesPerPixel);
            for (var x = 0; x < clipped.Width; x++, index += BytesPerPixel)
            {
                if (predicate(Bgra[index], Bgra[index + 1], Bgra[index + 2]))
                {
                    hits++;
                }
            }
        }

        return (double)hits / clipped.Area;
    }

    /// <summary>
    /// True if the frame is (almost) completely black, which indicates that the content could not
    /// be captured (e.g. exclusive fullscreen or protected content). Uses a sparse pixel sample.
    /// </summary>
    public bool IsBlack(int maxMeanBrightness = 4)
    {
        const int sampleStep = 97;   // prime, avoids sampling only one column
        long sum = 0;
        long count = 0;
        for (var i = 0; i + 2 < Bgra.Length; i += sampleStep * BytesPerPixel)
        {
            sum += Bgra[i] + Bgra[i + 1] + Bgra[i + 2];
            count++;
        }

        return count == 0 || sum / (count * 3) <= maxMeanBrightness;
    }
}
