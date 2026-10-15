// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Platform;
using OpenCvSharp;

namespace HellboardCompanion.Core.Recognition;

public enum SlotState
{
    Empty,
    Recognized,

    /// <summary>Something is in the slot, but no template matches clearly enough.</summary>
    Unknown,
}

/// <param name="StratagemId">Best match (also set for <see cref="SlotState.Unknown"/>, for diagnostics).</param>
/// <param name="Score">Normalized correlation of the best match (−1…1).</param>
/// <param name="Margin">Distance to the second-best stratagem.</param>
public sealed record SlotResult(SlotState State, string? StratagemId, double Score, double Margin, string? RunnerUpId)
{
    public override string ToString() => State switch
    {
        SlotState.Empty => "empty",
        SlotState.Recognized => $"{StratagemId} ({Score:F2}, +{Margin:F2})",
        _ => $"unknown (best {StratagemId} {Score:F2}, +{Margin:F2} over {RunnerUpId})",
    };
}

/// <summary>Visual style of a slot type, used to render matching templates from the SVG icons.</summary>
/// <param name="IconRatio">Icon size relative to the slot (measured on captures).</param>
/// <param name="Background">Slot background (BGR).</param>
/// <param name="ContentInset">Share of the slot excluded on each side when matching (frame decoration).</param>
public sealed record SlotStyle(double IconRatio, Scalar Background, double ContentInset)
{
    /// <summary>Player stratagem slots: icon 87.5 % of the slot interior on neutral gray (P4 measurement).</summary>
    public static SlotStyle PlayerSlot { get; } = new(0.875, new Scalar(47, 47, 47), 0);

    /// <summary>
    /// Mission stratagem slots: white icon 86.7 % of the slot on olive; the beige frame with its
    /// corner ornaments (≈ 15 % of the slot) is excluded (P4 measurement).
    /// </summary>
    public static SlotStyle MissionSlot { get; } = new(0.867, new Scalar(45, 59, 60), 0.15);
}

/// <summary>
/// Classifies the content of a stratagem slot by template matching against all icons, rendered
/// from the SVGs at a canonical size (implementation plan §6.4, §6.3.1). Resolution-independent:
/// the slot is scaled to the canonical size before matching.
/// </summary>
public sealed class StratagemSlotClassifier : IDisposable
{
    public const int CanonicalSize = 64;

    /// <summary>Tolerated misalignment in canonical pixels.</summary>
    private const int ShiftTolerance = 3;

    private readonly Dictionary<string, Mat> _templates;
    private readonly int _inset;
    private readonly double _minScore;
    private readonly double _minMargin;

    public StratagemSlotClassifier(IEnumerable<StratagemIcon> icons, SlotStyle style, double minScore = 0.7, double minMargin = 0.05)
    {
        _minScore = minScore;
        _minMargin = minMargin;
        _inset = (int)Math.Round(CanonicalSize * style.ContentInset);
        var iconSize = (int)Math.Round(CanonicalSize * style.IconRatio);
        var content = new Rect(_inset, _inset, CanonicalSize - (2 * _inset), CanonicalSize - (2 * _inset));
        _templates = icons
            .GroupBy(icon => icon.Id)
            .ToDictionary(g => g.Key, g =>
            {
                using var full = SvgRenderer.Render(g.First().SvgPath, CanonicalSize, iconSize, style.Background);
                return new Mat(full, content).Clone();
            });
    }

    public int TemplateCount => _templates.Count;

    /// <param name="frame">Capture of the game's client area.</param>
    /// <param name="slot">Slot interior in frame coordinates.</param>
    public SlotResult Classify(Frame frame, PixelRect slot)
    {
        // Slightly larger crop, so the templates can be matched with a small shift.
        var margin = (int)Math.Ceiling(slot.Width * ShiftTolerance / (double)CanonicalSize);
        var area = new PixelRect(slot.X - margin, slot.Y - margin, slot.Width + (2 * margin), slot.Height + (2 * margin));

        using var crop = FrameMat.CropBgr(frame, area);
        using var canonical = new Mat();
        var size = CanonicalSize + (2 * ShiftTolerance);
        Cv2.Resize(crop, canonical, new Size(size, size), interpolation: crop.Width > size ? InterpolationFlags.Area : InterpolationFlags.Cubic);

        if (IsEmpty(canonical))
        {
            return new SlotResult(SlotState.Empty, null, 0, 0, null);
        }

        // Search area: the slot content (without frame) plus the shift tolerance on each side.
        using var searchArea = new Mat(canonical, new Rect(_inset, _inset, size - (2 * _inset), size - (2 * _inset)));
        var ranking = _templates
            .Select(t => (Id: t.Key, Score: Match(searchArea, t.Value)))
            .OrderByDescending(r => r.Score)
            .Take(2)
            .ToList();

        var (bestId, best) = ranking[0];
        var (secondId, second) = ranking.Count > 1 ? ranking[1] : (null, -1);
        var margin2 = best - second;
        var state = best >= _minScore && margin2 >= _minMargin ? SlotState.Recognized : SlotState.Unknown;
        return new SlotResult(state, bestId, best, margin2, secondId);
    }

    public void Dispose()
    {
        foreach (var template in _templates.Values)
        {
            template.Dispose();
        }
    }

    private static double Match(Mat image, Mat template)
    {
        using var result = new Mat();
        Cv2.MatchTemplate(image, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double max);
        return max;
    }

    /// <summary>An empty slot is a uniform gray square; icons always add strong contrast.</summary>
    private static bool IsEmpty(Mat canonical)
    {
        var inner = new Rect(ShiftTolerance * 3, ShiftTolerance * 3, CanonicalSize - (ShiftTolerance * 4), CanonicalSize - (ShiftTolerance * 4));
        using var center = new Mat(canonical, inner);
        using var gray = new Mat();
        Cv2.CvtColor(center, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.MeanStdDev(gray, out _, out var stdDev);
        return stdDev.Val0 < 8;
    }
}
