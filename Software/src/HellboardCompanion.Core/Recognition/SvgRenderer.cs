// SPDX-License-Identifier: GPL-3.0-or-later

using OpenCvSharp;
using SkiaSharp;
using Svg.Skia;

namespace HellboardCompanion.Core.Recognition;

/// <summary>
/// Renders stratagem icons – SVG, or PNG with transparency (derived from a wiki icon with background) – for
/// the recognition (OpenCV, BGR 8 bit) and the user interface (Skia, transparent).
/// </summary>
public static class SvgRenderer
{
    /// <summary>
    /// Renders <paramref name="imagePath"/> scaled to <paramref name="iconSize"/>, centered on a
    /// <paramref name="canvasSize"/> square filled with <paramref name="background"/>.
    /// </summary>
    public static Mat Render(string imagePath, int canvasSize, int iconSize, Scalar background)
    {
        using var bitmap = new SKBitmap(canvasSize, canvasSize, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor((byte)background.Val2, (byte)background.Val1, (byte)background.Val0));
            var offset = (canvasSize - iconSize) / 2f;
            Draw(canvas, imagePath, new SKRect(offset, offset, offset + iconSize, offset + iconSize));
        }

        using var bgra = Mat.FromPixelData(canvasSize, canvasSize, MatType.CV_8UC4, bitmap.GetPixels());
        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }

    /// <summary>Renders <paramref name="imagePath"/> into a transparent <paramref name="size"/> square (user interface).</summary>
    public static SKBitmap RenderTransparent(string imagePath, int size)
    {
        var bitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        Draw(canvas, imagePath, new SKRect(0, 0, size, size));
        return bitmap;
    }

    /// <summary>
    /// The plain icon of a wiki icon with background (dark tile in the category color with a frame, e.g. the
    /// Meltagun, wiki-source-plan §4.1): rendered, the frame width measured from the left edge, frame and tile
    /// made transparent – anti-aliased edges by their distance to the tile color. Checked on all 107 stratagems
    /// that have both variants: drawn onto the slot background it equals the plain icon (≤ 0.6/255 at 64 px).
    /// </summary>
    /// <returns>PNG with transparency, <paramref name="size"/> square (the full icon frame like the plain SVGs).</returns>
    public static byte[] DeriveFromBackground(string backgroundSvgPath, int size = 256)
    {
        using var source = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using (var canvas = new SKCanvas(source))
        {
            canvas.Clear(new SKColor(255, 0, 255));   // only shows if the file has no tile of its own
            Draw(canvas, backgroundSvgPath, new SKRect(0, 0, size, size));
        }

        var frameColor = source.GetPixel(1, size / 2);
        var frame = 0;
        while (frame < size / 4 && Distance(source.GetPixel(frame, size / 2), frameColor) < 40)
        {
            frame++;
        }

        var tile = source.GetPixel(frame + 3, frame + 3);
        using var icon = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        icon.Erase(SKColors.Transparent);
        for (var y = frame + 1; y < size - frame - 1; y++)
        {
            for (var x = frame + 1; x < size - frame - 1; x++)
            {
                var pixel = source.GetPixel(x, y);
                var alpha = Math.Clamp(Distance(pixel, tile) / 60.0, 0, 1);
                icon.SetPixel(x, y, pixel.WithAlpha((byte)Math.Round(alpha * 255)));
            }
        }

        using var image = SKImage.FromBitmap(icon);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static void Draw(SKCanvas canvas, string imagePath, SKRect target)
    {
        if (imagePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            using var image = SKImage.FromEncodedData(imagePath) ?? throw new InvalidDataException($"Cannot read image '{imagePath}'.");
            canvas.DrawImage(image, target, new SKSamplingOptions(SKCubicResampler.Mitchell));
            return;
        }

        using var svg = new SKSvg();
        var picture = svg.Load(imagePath) ?? throw new InvalidDataException($"Cannot read SVG '{imagePath}'.");
        var viewBox = picture.CullRect;
        canvas.Save();
        canvas.Translate(target.Left, target.Top);
        canvas.Scale(target.Width / Math.Max(viewBox.Width, viewBox.Height));
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private static double Distance(SKColor a, SKColor b) =>
        Math.Sqrt(Math.Pow(a.Red - b.Red, 2) + Math.Pow(a.Green - b.Green, 2) + Math.Pow(a.Blue - b.Blue, 2));
}
