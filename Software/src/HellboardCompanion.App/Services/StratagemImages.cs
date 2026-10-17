// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;
using SkiaSharp;
using Svg.Skia;

namespace HellboardCompanion.App.Services;

/// <summary>
/// Stratagem icons for the user interface, rendered from the downloaded SVGs and cached. Stratagems
/// without an icon in the icon repository (e.g. mission objectives like SSSD Delivery) get a placeholder:
/// a tile in their category color with their initials.
/// </summary>
public sealed class StratagemImages
{
    private const int Size = 96;

    private readonly StratagemIconStore? _iconStore;
    private readonly StratagemCatalog? _catalog;
    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new();

    public StratagemImages(StratagemIconStore? iconStore, StratagemCatalog? catalog = null)
    {
        _iconStore = iconStore;
        _catalog = catalog;
        if (iconStore is not null)
        {
            iconStore.IconsChanged += (_, _) => _cache.Clear();   // icons were added or updated (§6.11)
        }

        if (catalog is not null)
        {
            catalog.Changed += (_, _) => _cache.Clear();   // name or category of a placeholder may have changed (§6.12)
        }
    }

    /// <summary>The icon of a stratagem, a placeholder if the icon repository has none, or <c>null</c> if unknown.</summary>
    public Bitmap? Get(string? id)
    {
        if (id is null)
        {
            return null;
        }

        if (_cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        if (_iconStore?.Icons.FirstOrDefault(i => i.Id == id) is { } icon)
        {
            return _cache[id] = Render(icon.SvgPath);
        }

        // Only once the icons are loaded: on the first start they may still be downloading.
        if ((_iconStore is null || _iconStore.Icons.Count > 0) && _catalog?.Find(id) is { } entry)
        {
            return _cache[id] = Placeholder(entry.Name, entry.Color);
        }

        return null;
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

            return ToBitmap(bitmap);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A tile in the category color with the initials of the name, e.g. "SD" for SSSD Delivery.</summary>
    private static Bitmap? Placeholder(string name, string color)
    {
        try
        {
            var tint = color switch
            {
                "red" => new SKColor(0xDE, 0x7B, 0x6C),
                "blue" => new SKColor(0x5B, 0xA8, 0xDA),
                "green" => new SKColor(0x7B, 0xB8, 0x6B),
                "yellow" => new SKColor(0xE6, 0xC4, 0x4C),
                _ => new SKColor(0xC8, 0xC8, 0xC8),
            };

            using var bitmap = new SKBitmap(Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                var tile = new SKRect(8, 8, Size - 8, Size - 8);
                using var fill = new SKPaint { Color = tint.WithAlpha(0x40), IsAntialias = true };
                using var border = new SKPaint { Color = tint, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
                canvas.DrawRoundRect(tile, 12, 12, fill);
                canvas.DrawRoundRect(tile, 12, 12, border);

                using var typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
                using var font = new SKFont(typeface, 34);
                using var text = new SKPaint { Color = tint, IsAntialias = true };
                var initials = Initials(name);
                var width = font.MeasureText(initials, text);
                font.GetFontMetrics(out var metrics);
                canvas.DrawText(initials, (Size - width) / 2, (Size - metrics.Ascent - metrics.Descent) / 2, font, text);
            }

            return ToBitmap(bitmap);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Initials(string name)
    {
        var letters = name.Split(' ', '-', '/')
            .Select(word => word.FirstOrDefault(char.IsLetter))
            .Where(c => c != default)
            .Take(2)
            .Select(char.ToUpperInvariant);
        return string.Concat(letters) is { Length: > 0 } initials ? initials : "?";
    }

    private static Bitmap ToBitmap(SKBitmap bitmap)
    {
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = png.AsStream();
        return new Bitmap(stream);
    }
}
