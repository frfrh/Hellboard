// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Layout;

/// <summary>
/// Maps layout coordinates to screen pixels (implementation plan §6.3.1): the game scales its UI by
/// the limiting dimension and anchors the player card top-left. Verified for 16:9 and 16:10.
/// </summary>
public sealed class LayoutScaler
{
    public LayoutScaler(LoadoutLayout layout, PixelRect clientBounds)
    {
        ClientBounds = clientBounds;
        Scale = Math.Min(
            (double)clientBounds.Width / layout.ReferenceWidth,
            (double)clientBounds.Height / layout.ReferenceHeight);
    }

    public PixelRect ClientBounds { get; }

    /// <summary>Factor from reference to screen pixels, e.g. 1.333 at 2560×1440.</summary>
    public double Scale { get; }

    /// <summary>Screen rectangle of a layout element, rounded outward to whole pixels.</summary>
    public PixelRect ToScreen(LayoutRect rect)
    {
        var left = (int)Math.Floor(ClientBounds.X + (rect.X * Scale));
        var top = (int)Math.Floor(ClientBounds.Y + (rect.Y * Scale));
        var right = (int)Math.Ceiling(ClientBounds.X + ((rect.X + rect.Width) * Scale));
        var bottom = (int)Math.Ceiling(ClientBounds.Y + ((rect.Y + rect.Height) * Scale));
        return PixelRect.FromEdges(left, top, right, bottom);
    }
}
