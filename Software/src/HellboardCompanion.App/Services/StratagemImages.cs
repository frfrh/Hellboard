// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using HellboardCompanion.Core.Catalog;
using SkiaSharp;
using Svg.Skia;

namespace HellboardCompanion.App.Services;

/// <summary>Stratagem icons for the user interface, rendered from the downloaded SVGs and cached.</summary>
public sealed class StratagemImages(StratagemIconStore? iconStore)
{
    private const int Size = 96;

    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new();

    /// <summary>The icon of a stratagem, or <c>null</c> if no icon is available (yet).</summary>
    public Bitmap? Get(string? id)
    {
        if (id is null || iconStore is null)
        {
            return null;
        }

        if (_cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var icon = iconStore.Icons.FirstOrDefault(i => i.Id == id);
        if (icon is null)
        {
            return null;   // not cached: the icons may still be downloading
        }

        return _cache[id] = Render(icon.SvgPath);
    }

    private static Bitmap? Render(string svgPath)
    {
        try
        {
            using var svg = new SKSvg();
            var picture = svg.Load(svgPath);
            if (picture is null)
            {
                return null;
            }

            using var bitmap = new SKBitmap(Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(Size / Math.Max(picture.CullRect.Width, picture.CullRect.Height));
                canvas.DrawPicture(picture);
            }

            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = png.AsStream();
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
