// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>
/// Rectangle in physical screen pixels. Coordinates can be negative (monitors left of or above
/// the primary monitor).
/// </summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public long Area => (long)Math.Max(Width, 0) * Math.Max(Height, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        return right > left && bottom > top ? FromEdges(left, top, right, bottom) : default;
    }

    /// <summary>Squared distance between the centers, used to find the nearest monitor.</summary>
    public long CenterDistanceSquared(PixelRect other)
    {
        long dx = (X * 2L + Width) - (other.X * 2L + other.Width);
        long dy = (Y * 2L + Height) - (other.Y * 2L + other.Height);
        return dx * dx + dy * dy;
    }

    public override string ToString() => $"{Width}×{Height} at ({X}, {Y})";
}
