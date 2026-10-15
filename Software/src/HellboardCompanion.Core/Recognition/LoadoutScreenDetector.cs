// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Recognition;

/// <param name="IsVisible">Whether the loadout screen (local player's card) is shown.</param>
/// <param name="Offset">Position correction of the layout, found via the ready button.</param>
/// <param name="ButtonScore">Cue A: ready button geometry and stripe pattern (0…1).</param>
/// <param name="GridScore">Cue B: player slot raster – gray slot edges and black gaps (0…1).</param>
/// <param name="MissionFrameScore">Cue D: beige frames of the fixed mission stratagems (0…1, supporting).</param>
public sealed record LoadoutScreenResult(
    bool IsVisible,
    PixelOffset Offset,
    double ButtonScore,
    double GridScore,
    double MissionFrameScore)
{
    public static LoadoutScreenResult NotVisible { get; } = new(false, default, 0, 0, 0);
}

/// <summary>
/// Decides whether the loadout screen is visible, using only structural cues that do not depend on
/// the selected stratagems, the mission or the game language (implementation plan §6.4):
/// A – ready button (yellow outline or fill, diagonal stripes), B – player slot raster,
/// D – beige frames of the fixed mission stratagems (supporting only).
/// </summary>
public sealed class LoadoutScreenDetector(LoadoutLayout layout)
{
    /// <summary>Search margin around the expected button position, relative to the client height.</summary>
    public const double SearchMargin = 0.02;

    // Measured on all local scenes, 720p-4K (P4): loadout screen button 0.86-1.00 / grid 0.61-0.99,
    // other screens button 0.00 / grid <= 0.05.
    private const double ButtonThreshold = 0.6;
    private const double GridThreshold = 0.4;
    private const double MissionFrameThreshold = 0.6;
    private const double StrongCueThreshold = 0.85;

    /// <param name="frame">Capture of the game's client area.</param>
    public LoadoutScreenResult Detect(Frame frame)
    {
        var scaler = new LayoutScaler(layout, new PixelRect(0, 0, frame.Width, frame.Height));
        var expectedButton = scaler.ToScreen(layout.ReadyButton);
        var margin = Math.Max(2, (int)Math.Round(frame.Height * SearchMargin));

        var (buttonScore, offset) = EvaluateButton(frame, expectedButton, margin, scaler.Scale);
        var gridScore = EvaluateGrid(frame, scaler, offset);
        var missionScore = EvaluateMissionFrames(frame, scaler, offset);

        var visible = (buttonScore >= ButtonThreshold && gridScore >= GridThreshold)
            || ((buttonScore >= StrongCueThreshold || gridScore >= StrongCueThreshold) && missionScore >= MissionFrameThreshold);

        return new LoadoutScreenResult(visible, offset, buttonScore, gridScore, missionScore);
    }

    // ---- Cue A: ready button --------------------------------------------------------------

    private static (double Score, PixelOffset Offset) EvaluateButton(Frame frame, PixelRect expected, int margin, double scale)
    {
        var window = new PixelRect(expected.X - margin, expected.Y - margin, expected.Width + (2 * margin), expected.Height + (2 * margin))
            .Intersect(new PixelRect(0, 0, frame.Width, frame.Height));
        if (window.IsEmpty)
        {
            return (0, default);
        }

        // Rows/columns that are mostly yellow: the outline (Prepare) or the fill (Ready).
        var rowCounts = new int[window.Height];
        var columnCounts = new int[window.Width];
        for (var y = 0; y < window.Height; y++)
        {
            for (var x = 0; x < window.Width; x++)
            {
                if (IsYellow(frame, window.X + x, window.Y + y))
                {
                    rowCounts[y]++;
                    columnCounts[x]++;
                }
            }
        }

        var top = Array.FindIndex(rowCounts, c => c >= expected.Width / 2);
        var bottom = Array.FindLastIndex(rowCounts, c => c >= expected.Width / 2);
        if (top < 0 || bottom <= top)
        {
            return (0, default);
        }

        var minColumn = (bottom - top + 1) / 2;
        var left = Array.FindIndex(columnCounts, c => c >= minColumn);
        var right = Array.FindLastIndex(columnCounts, c => c >= minColumn);
        if (left < 0 || right <= left)
        {
            return (0, default);
        }

        var found = PixelRect.FromEdges(window.X + left, window.Y + top, window.X + right + 1, window.Y + bottom + 1);
        var sizeScore = Math.Min(Ratio(found.Width, expected.Width), Ratio(found.Height, expected.Height));

        // Center-based: the fill (Ready) is a few pixels smaller than the outline (Prepare).
        var offset = new PixelOffset(
            (int)Math.Round(((found.X * 2) + found.Width - (expected.X * 2) - expected.Width) / 2.0),
            (int)Math.Round(((found.Y * 2) + found.Height - (expected.Y * 2) - expected.Height) / 2.0));

        // Prepare: dark button with diagonal stripes. Ready: mostly yellow fill (the label covers the rest).
        var fillScore = Math.Clamp((frame.Share(found, (b, g, r) => IsYellow(b, g, r)) - 0.4) / 0.2, 0, 1);
        var patternScore = Math.Max(StripeScore(frame, found, scale), fillScore);

        return (Math.Min(sizeScore, patternScore), offset);
    }

    /// <summary>
    /// Diagonal stripes over the whole button: luminance changes little along the stripes and a lot
    /// across them. Evaluated per block, so the label and key symbol (whose position depends on the
    /// game language) only affect a minority of blocks. Direction-independent.
    /// </summary>
    private static double StripeScore(Frame frame, PixelRect button, double scale)
    {
        var step = Math.Max(1, (int)Math.Round(3 * scale));              // ~4 px at 1440p, a quarter stripe period
        var blockSize = Math.Max(6, (int)Math.Round(14 * scale));        // ~one stripe period
        var area = new PixelRect(
            button.X + (int)(button.Width * 0.02),
            button.Y + (int)(button.Height * 0.2),
            (int)(button.Width * 0.96),
            (int)(button.Height * 0.6));

        var blocks = 0;
        var stripedBlocks = 0;
        for (var by = area.Y; by + blockSize <= area.Bottom; by += blockSize)
        {
            for (var bx = area.X; bx + blockSize <= area.Right; bx += blockSize)
            {
                long rising = 0;
                long falling = 0;
                for (var y = Math.Max(by, step); y < Math.Min(by + blockSize, frame.Height - step); y++)
                {
                    for (var x = bx; x < Math.Min(bx + blockSize, frame.Width - step); x++)
                    {
                        var l = Luminance(frame, x, y);
                        rising += Math.Abs(l - Luminance(frame, x + step, y - step));
                        falling += Math.Abs(l - Luminance(frame, x + step, y + step));
                    }
                }

                blocks++;
                var ratio = (double)Math.Max(rising, falling) / Math.Max(Math.Min(rising, falling), 1);
                if (ratio >= 1.5)
                {
                    stripedBlocks++;
                }
            }
        }

        // Half of the blocks clearly striped is enough for a full score.
        return blocks == 0 ? 0 : Math.Clamp(stripedBlocks / (blocks * 0.5), 0, 1);
    }

    // ---- Cue B: player slot raster --------------------------------------------------------

    /// <summary>
    /// Each player slot has a uniform neutral gray background along its inner edge (icons keep a
    /// margin), whether it is empty or filled. The gaps between the slots are transparent (the
    /// animated background shows through), so they are not used.
    /// </summary>
    private double EvaluateGrid(Frame frame, LayoutScaler scaler, PixelOffset offset)
    {
        var slots = layout.PlayerSlots.Select(s => Shift(scaler.ToScreen(s), offset)).ToList();
        var inset = Math.Max(1, (int)Math.Round(slots[0].Width * 0.04));

        return slots.Min(slot =>
        {
            var inner = new PixelRect(slot.X + inset, slot.Y + inset, slot.Width - (2 * inset), slot.Height - (2 * inset));
            return RingShare(frame, inner, Math.Max(1, inset), IsSlotGray);
        });
    }

    // ---- Cue D: fixed mission stratagem frames --------------------------------------------

    private double EvaluateMissionFrames(Frame frame, LayoutScaler scaler, PixelOffset offset)
    {
        var thickness = Math.Max(1, (int)Math.Round(2 * scaler.Scale));
        return layout.FixedMissionSlots
            .Select(s => RingShare(frame, Shift(scaler.ToScreen(s), offset), thickness, IsBeige))
            .Average();
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static PixelRect Shift(PixelRect rect, PixelOffset offset) => rect with { X = rect.X + offset.X, Y = rect.Y + offset.Y };

    /// <summary>Share of matching pixels in a ring of <paramref name="thickness"/> along the inside of <paramref name="rect"/>.</summary>
    private static double RingShare(Frame frame, PixelRect rect, int thickness, Func<byte, byte, byte, bool> predicate)
    {
        var top = new PixelRect(rect.X, rect.Y, rect.Width, thickness);
        var bottom = new PixelRect(rect.X, rect.Bottom - thickness, rect.Width, thickness);
        var left = new PixelRect(rect.X, rect.Y + thickness, thickness, rect.Height - (2 * thickness));
        var right = new PixelRect(rect.Right - thickness, rect.Y + thickness, thickness, rect.Height - (2 * thickness));

        double hits = 0;
        long total = 0;
        foreach (var side in new[] { top, bottom, left, right })
        {
            hits += frame.Share(side, predicate) * side.Area;
            total += side.Area;
        }

        return total == 0 ? 0 : hits / total;
    }

    private static double Ratio(int a, int b) => a <= 0 || b <= 0 ? 0 : (double)Math.Min(a, b) / Math.Max(a, b);

    private static bool IsYellow(Frame frame, int x, int y)
    {
        var i = (y * frame.Stride) + (x * Frame.BytesPerPixel);
        return IsYellow(frame.Bgra[i], frame.Bgra[i + 1], frame.Bgra[i + 2]);
    }

    /// <summary>
    /// Clearly yellow, also when a thin outline is blended with the dark background by scaling;
    /// the olive stripes of the Prepare button (≈ 90, 95, 27) are excluded.
    /// </summary>
    private static bool IsYellow(byte b, byte g, byte r) => r > 100 && r - b > 85 && g - b > 75;

    private static int Luminance(Frame frame, int x, int y)
    {
        var i = (y * frame.Stride) + (x * Frame.BytesPerPixel);
        return (frame.Bgra[i] + (5 * frame.Bgra[i + 1]) + (2 * frame.Bgra[i + 2])) >> 3;
    }

    private static bool IsSlotGray(byte b, byte g, byte r)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        return max is >= 30 and <= 70 && max - min <= 12;
    }

    private static bool IsBeige(byte b, byte g, byte r) => r > 140 && g > 120 && b < 150 && r - b > 35;
}
