// SPDX-License-Identifier: GPL-3.0-or-later

using OpenCvSharp;
using SkiaSharp;
using Svg.Skia;

namespace HellboardCompanion.Core.Recognition;

/// <summary>Renders SVG icons into OpenCV images (BGR, 8 bit).</summary>
public static class SvgRenderer
{
    /// <summary>
    /// Renders <paramref name="svgPath"/> scaled to <paramref name="iconSize"/>, centered on a
    /// <paramref name="canvasSize"/> square filled with <paramref name="background"/>.
    /// </summary>
    public static Mat Render(string svgPath, int canvasSize, int iconSize, Scalar background)
    {
        using var svg = new SKSvg();
        var picture = svg.Load(svgPath) ?? throw new InvalidDataException($"Cannot read SVG '{svgPath}'.");
        var viewBox = picture.CullRect;

        using var bitmap = new SKBitmap(canvasSize, canvasSize, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor((byte)background.Val2, (byte)background.Val1, (byte)background.Val0));
            var offset = (canvasSize - iconSize) / 2f;
            canvas.Translate(offset, offset);
            canvas.Scale(iconSize / Math.Max(viewBox.Width, viewBox.Height));
            canvas.DrawPicture(picture);
        }

        using var bgra = Mat.FromPixelData(canvasSize, canvasSize, MatType.CV_8UC4, bitmap.GetPixels());
        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }
}
