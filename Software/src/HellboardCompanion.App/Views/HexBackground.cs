// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HellboardCompanion.App.Views;

/// <summary>
/// Window background in the style of Helldivers 2 (UI polish plan §1, option 1 + 2): a dark base with a
/// soft vignette, a fine hexagon grid (the hexagon of the app icon and the display logo), a faint yellow
/// glow in the top-left corner and yellow/black hazard stripes along the bottom edge that fade out
/// upwards. Drawn for the current size: the pattern repeats instead of being stretched, the
/// decorations stay at their edges.
/// </summary>
public sealed class HexBackground : Control
{
    private const double HexRadius = 26;
    private const double StripeBand = 130;
    private const double StripeWidth = 16;

    private static readonly Color SuperEarthYellow = Color.FromRgb(0xF5, 0xD3, 0x3B);
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(9, 255, 255, 255)), 1).ToImmutable();
    private static readonly IBrush StripeBrush = new SolidColorBrush(Color.FromArgb(20, SuperEarthYellow.R, SuperEarthYellow.G, SuperEarthYellow.B)).ToImmutable();
    private static readonly IBrush TopLine = new SolidColorBrush(Color.FromArgb(60, SuperEarthYellow.R, SuperEarthYellow.G, SuperEarthYellow.B)).ToImmutable();

    private Size _geometrySize;
    private Geometry? _grid;
    private Geometry? _stripes;

    static HexBackground() => AffectsRender<HexBackground>(BoundsProperty);

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        if (size != _geometrySize)
        {
            _grid = HexGrid(size);
            _stripes = Stripes(size);
            _geometrySize = size;
        }

        var all = new Rect(size);

        // Dark, slightly cool base with a vignette that scales with the window.
        context.FillRectangle(new RadialGradientBrush
        {
            Center = new RelativePoint(0.35, 0.2, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.35, 0.2, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.9 * Math.Max(size.Width, size.Height), RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(0.9 * Math.Max(size.Width, size.Height), RelativeUnit.Absolute),
            GradientStops = { new GradientStop(Color.FromRgb(0x1A, 0x1C, 0x20), 0), new GradientStop(Color.FromRgb(0x0B, 0x0C, 0x0E), 1) },
        }, all);

        context.DrawGeometry(null, GridPen, _grid!);

        // Faint Super Earth yellow glow in the top-left corner.
        context.FillRectangle(new RadialGradientBrush
        {
            Center = new RelativePoint(0, 0, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(420, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(420, RelativeUnit.Absolute),
            GradientStops = { new GradientStop(Color.FromArgb(18, SuperEarthYellow.R, SuperEarthYellow.G, SuperEarthYellow.B), 0), new GradientStop(Colors.Transparent, 1) },
        }, all);

        // Hazard stripes along the bottom edge, fading out upwards (opacity mask, no hard edge).
        var band = new Rect(0, size.Height - StripeBand, size.Width, StripeBand);
        using (context.PushOpacityMask(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, 1) },
        }, band))
        {
            context.DrawGeometry(StripeBrush, null, _stripes!);
        }

        // Thin warning line along the top edge.
        context.FillRectangle(TopLine, new Rect(0, 0, size.Width, 2));
    }

    /// <summary>Flat-topped hexagons of a fixed size; the window shows more or fewer of them.</summary>
    private static Geometry HexGrid(Size size)
    {
        var dx = HexRadius * 1.5;
        var dy = Math.Sqrt(3) * HexRadius;
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        for (var column = -1; column * dx < size.Width + HexRadius; column++)
        {
            for (var row = -1; row * dy < size.Height + dy; row++)
            {
                var center = new Point(column * dx, (row * dy) + (column % 2 == 0 ? 0 : dy / 2));
                for (var k = 0; k <= 6; k++)
                {
                    var angle = Math.PI / 3 * k;
                    var point = new Point(center.X + (HexRadius * Math.Cos(angle)), center.Y + (HexRadius * Math.Sin(angle)));
                    if (k == 0)
                    {
                        context.BeginFigure(point, false);
                    }
                    else
                    {
                        context.LineTo(point);
                    }
                }

                context.EndFigure(false);
            }
        }

        return geometry;
    }

    /// <summary>Diagonal stripes in the bottom band.</summary>
    private static Geometry Stripes(Size size)
    {
        var bottom = size.Height;
        var top = size.Height - StripeBand;
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        for (var x = -StripeBand; x < size.Width + StripeBand; x += StripeWidth * 2)
        {
            context.BeginFigure(new Point(x, bottom), true);
            context.LineTo(new Point(x + StripeWidth, bottom));
            context.LineTo(new Point(x + StripeWidth + StripeBand, top));
            context.LineTo(new Point(x + StripeBand, top));
            context.EndFigure(true);
        }

        return geometry;
    }
}
