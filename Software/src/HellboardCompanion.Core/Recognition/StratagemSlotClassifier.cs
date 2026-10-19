// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
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
/// <param name="Margin">Distance to the second-best stratagem; for close candidates decided by the
/// pixels where their icons differ, the relative lead there.</param>
public sealed record SlotResult(SlotState State, string? StratagemId, double Score, double Margin, string? RunnerUpId)
{
    public override string ToString() => State switch
    {
        SlotState.Empty => "empty",
        SlotState.Recognized => $"{StratagemId} ({Score:F2}, +{Margin:F2})",
        _ => $"unknown (best {StratagemId} {Score:F2}, +{Margin:F2} over {RunnerUpId})",
    };
}

/// <summary>Visual style of a slot type, used to render matching templates from the icons.</summary>
/// <param name="IconRatio">Icon frame size relative to the slot (measured on captures).</param>
/// <param name="Background">Slot background (BGR).</param>
/// <param name="ContentInset">Share of the slot excluded on each side when matching (frame decoration).</param>
public sealed record SlotStyle(double IconRatio, Scalar Background, double ContentInset)
{
    /// <summary>
    /// Player stratagem slots: the wiki icons are traced on the 256×256 frame of the game texture, so the icon
    /// frame fills the slot interior (wiki-source-plan §1.1; the former repository icons were cropped: 87.5 %).
    /// </summary>
    public static SlotStyle PlayerSlot { get; } = new(1.0, new Scalar(47, 47, 47), 0);

    /// <summary>
    /// Mission stratagem slots: white icon on olive, the icon frame 99.1 % of the slot (P4 measurement 86.7 % with
    /// the cropped repository icons, same factor); the beige frame with its corner ornaments (≈ 15 % of the slot)
    /// is excluded.
    /// </summary>
    public static SlotStyle MissionSlot { get; } = new(0.991, new Scalar(45, 59, 60), 0.15);
}

/// <summary>
/// Classifies the content of a stratagem slot by template matching against all icons, rendered
/// from the SVGs at a canonical size (implementation plan §6.4, §6.3.1). Resolution-independent:
/// the slot is scaled to the canonical size before matching.
/// </summary>
public sealed class StratagemSlotClassifier : IDisposable
{
    public const int CanonicalSize = 64;

    /// <summary>
    /// Tolerated misalignment in canonical pixels. The game places some icons a few pixels away from
    /// the position of their SVG (Guard Dog Rover: 5 px higher).
    /// </summary>
    private const int ShiftTolerance = 6;

    /// <summary>Candidates from the coarse pass that are compared at full resolution.</summary>
    private const int FineCandidates = 6;

    /// <summary>
    /// Relative lead on the pixels where two close candidates differ that decides between them (icon
    /// variants with the same body, e.g. the Guard Dog family or the shield backpacks, only differ in a
    /// small emblem).
    /// </summary>
    private const double DetailMargin = 0.1;

    /// <summary>Score the best candidate needs before the pixels where the close ones differ may decide.</summary>
    private const double DetailMinScore = 0.85;

    /// <summary>Icon sizes of the second pass, relative to <see cref="SlotStyle.IconRatio"/>.</summary>
    private static readonly double[] SizeTolerances = [0.95, 1.05];

    /// <summary>Gray-level difference above which a pixel belongs to the region where two templates differ.</summary>
    private const int DetailThreshold = 30;

    /// <summary>Mean absolute pixel difference below which a slot counts as unchanged.</summary>
    private const double UnchangedThreshold = 3.0;

    private readonly Dictionary<string, Mat[]> _templates;   // per stratagem: its reference images, otherwise its SVG
    private readonly Dictionary<string, Mat[]> _coarseTemplates;
    private readonly Dictionary<string, Mat[]> _scaledTemplates;
    private readonly ConcurrentDictionary<PixelRect, (Mat Canonical, SlotResult Result)> _cache = new();
    private readonly int _inset;
    private readonly double _minScore;
    private readonly double _minMargin;

    /// <param name="references">Slot images from the game assigned by the user (same slot kind as
    /// <paramref name="style"/>); they are compared instead of the SVG of their stratagem.</param>
    public StratagemSlotClassifier(IEnumerable<StratagemIcon> icons, SlotStyle style, double minScore = 0.7, double minMargin = 0.05,
        IEnumerable<StratagemReference>? references = null)
    {
        _minScore = minScore;
        _minMargin = minMargin;
        _inset = (int)Math.Round(CanonicalSize * style.ContentInset);
        var iconSize = (int)Math.Round(CanonicalSize * style.IconRatio);
        var content = new Rect(_inset, _inset, CanonicalSize - (2 * _inset), CanonicalSize - (2 * _inset));
        Mat Render(string svgPath, int size)
        {
            using var full = SvgRenderer.Render(svgPath, CanonicalSize, size, style.Background);
            return new Mat(full, content).Clone();
        }

        // Reference images replace the SVG of their stratagem: they show how the game really draws it, and a
        // wrong SVG (Bastion MK XVI shows the Maelstrom) would otherwise still compete with the right stratagem.
        var images = (references ?? [])
            .Select(r => (r.Id, Template: LoadReference(r.Path, content)))
            .Where(r => r.Template is not null)
            .ToLookup(r => r.Id, r => r.Template!);
        // Icons shared by several stratagems cannot tell them apart (wiki-source-plan §4.2).
        var unique = icons.Where(icon => icon.Recognizable).GroupBy(icon => icon.Id).Where(g => !images.Contains(g.Key))
            .ToDictionary(g => g.Key, g => g.First().ImagePath);
        _templates = unique.ToDictionary(i => i.Key, i => new[] { Render(i.Value, iconSize) });
        foreach (var group in images)
        {
            _templates[group.Key] = group.ToArray();
        }

        _coarseTemplates = _templates.ToDictionary(t => t.Key, t => t.Value.Select(Half).ToArray());
        _scaledTemplates = unique.ToDictionary(i => i.Key, i => SizeTolerances.Select(s => Render(i.Value, (int)Math.Round(iconSize * s))).ToArray());
    }

    public int TemplateCount => _templates.Count;

    /// <summary>The slot content of a saved reference image at canonical size, <c>null</c> if it cannot be read.</summary>
    private static Mat? LoadReference(string path, Rect content)
    {
        using var image = Cv2.ImRead(path, ImreadModes.Color);
        if (image.Empty())
        {
            return null;
        }

        var (offset, size) = StratagemReference.SlotIn(image.Width);
        if (offset + size > image.Height)
        {
            return null;
        }

        using var slot = new Mat(image, new Rect(offset, offset, size, size));
        using var canonical = new Mat();
        Cv2.Resize(slot, canonical, new Size(CanonicalSize, CanonicalSize), interpolation: size > CanonicalSize ? InterpolationFlags.Area : InterpolationFlags.Cubic);
        return new Mat(canonical, content).Clone();
    }

    /// <param name="frame">Capture of the game's client area.</param>
    /// <param name="slot">Slot interior in frame coordinates.</param>
    public SlotResult Classify(Frame frame, PixelRect slot)
    {
        // Slightly larger crop, so the templates can be matched with a small shift.
        var margin = (int)Math.Ceiling(slot.Width * ShiftTolerance / (double)CanonicalSize);
        var area = new PixelRect(slot.X - margin, slot.Y - margin, slot.Width + (2 * margin), slot.Height + (2 * margin));

        using var crop = FrameMat.CropBgr(frame, area);
        using var canonical = new Mat();

        // Same scale as the slot itself (the rounded-up margin would otherwise shrink small slots by a few percent).
        var size = (int)Math.Round(area.Width * (double)CanonicalSize / slot.Width);
        Cv2.Resize(crop, canonical, new Size(size, size), interpolation: crop.Width > size ? InterpolationFlags.Area : InterpolationFlags.Cubic);

        // The slot content rarely changes between frames: reuse the last result for the same position.
        if (_cache.TryGetValue(slot, out var cached) && MeanAbsDiff(cached.Canonical, canonical) < UnchangedThreshold)
        {
            return cached.Result;
        }

        var result = ClassifyCanonical(canonical, size);
        if (_cache.Count > 64)
        {
            ClearCache();
        }

        if (_cache.TryRemove(slot, out var previous))
        {
            previous.Canonical.Dispose();
        }

        _cache[slot] = (canonical.Clone(), result);
        return result;
    }

    private SlotResult ClassifyCanonical(Mat canonical, int size)
    {
        if (IsEmpty(canonical, (size - CanonicalSize) / 2))
        {
            return new SlotResult(SlotState.Empty, null, 0, 0, null);
        }

        // Search area: the slot content (without frame) plus the shift tolerance on each side.
        using var searchArea = new Mat(canonical, new Rect(_inset, _inset, size - (2 * _inset), size - (2 * _inset)));

        // Coarse pass at half resolution over all templates, fine pass with full shift search on the best few.
        using var coarseSearchArea = Half(searchArea);
        // A stratagem counts with its best template (several reference images, or the SVG).
        var ids = _coarseTemplates
            .Select(t => (Id: t.Key, Score: t.Value.Max(template => Match(coarseSearchArea, template))))
            .OrderByDescending(r => r.Score)
            .Take(FineCandidates)
            .Select(r => r.Id)
            .ToList();
        var ranking = ids
            .Select(id => _templates[id].Select(template => BestMatch(searchArea, id, template)).MaxBy(c => c.Score)!)
            .OrderByDescending(c => c.Score)
            .ToList();
        if (Decide(searchArea, ranking) is { } result)
        {
            return result;
        }

        // Second pass: the game draws some icons slightly larger or smaller than their SVG (Warp Pack: 5 % larger).
        ranking = ranking
            .Select(plain => _scaledTemplates.GetValueOrDefault(plain.Id, []).Select(template => BestMatch(searchArea, plain.Id, template)).Append(plain).MaxBy(c => c.Score)!)
            .OrderByDescending(c => c.Score)
            .ToList();
        if (Decide(searchArea, ranking) is { } scaled)
        {
            return scaled;
        }

        var second = ranking.ElementAtOrDefault(1);
        return new SlotResult(SlotState.Unknown, ranking[0].Id, ranking[0].Score, ranking[0].Score - (second?.Score ?? -1), second?.Id);
    }

    /// <summary>The recognized stratagem of a ranking (best first), <c>null</c> if it is not clear.</summary>
    private SlotResult? Decide(Mat searchArea, IReadOnlyList<Candidate> ranking)
    {
        var best = ranking[0];
        var second = ranking.ElementAtOrDefault(1);
        var margin = best.Score - (second?.Score ?? -1);
        if (best.Score >= _minScore && margin >= _minMargin)
        {
            return new SlotResult(SlotState.Recognized, best.Id, best.Score, margin, second?.Id);
        }

        // Close candidates: decide by the pixels where their templates differ. Only when the icon as a
        // whole fits well – otherwise the alignment is unreliable (Expendable Anti-Tank with its game-only
        // badge would become Recoilless Rifle).
        var close = ranking.Where(c => c.Score >= _minScore && best.Score - c.Score < _minMargin).ToList();
        if (best.Score >= DetailMinScore && close.Count > 1 && DecideByDetail(searchArea, close) is { } decided)
        {
            return new SlotResult(SlotState.Recognized, decided.Winner.Id, decided.Winner.Score, decided.Lead, decided.RunnerUpId);
        }

        return null;
    }

    /// <summary>
    /// The candidate that leads every other one by at least <see cref="DetailMargin"/> on the pixels
    /// where their two templates differ; <c>null</c> if there is none.
    /// </summary>
    private static (Candidate Winner, double Lead, string RunnerUpId)? DecideByDetail(Mat searchArea, IReadOnlyList<Candidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            var weakest = candidates
                .Where(other => other != candidate)
                .Select(other => (Other: other.Id, Lead: DetailLead(searchArea, candidate, other)))
                .MinBy(l => l.Lead);
            if (weakest.Lead >= DetailMargin)
            {
                return (candidate, weakest.Lead, weakest.Other);
            }
        }

        return null;
    }

    /// <summary>
    /// Compares two candidates on the pixels where their templates differ, both at the position of the
    /// stronger overall match (the common parts of the icon align it reliably). Returns the relative
    /// lead of <paramref name="candidate"/>: (error of other − own error) / (sum of both), −1…1.
    /// </summary>
    private static double DetailLead(Mat searchArea, Candidate candidate, Candidate other)
    {
        using var difference = new Mat();
        Cv2.Absdiff(candidate.Template, other.Template, difference);
        using var gray = new Mat();
        Cv2.CvtColor(difference, gray, ColorConversionCodes.BGR2GRAY);
        using var region = new Mat();
        Cv2.Threshold(gray, region, DetailThreshold, 255, ThresholdTypes.Binary);
        if (Cv2.CountNonZero(region) == 0)
        {
            return 0;
        }

        var location = (candidate.Score >= other.Score ? candidate : other).Location;
        using var patch = new Mat(searchArea, new Rect(location, candidate.Template.Size()));
        var own = MeanError(patch, candidate.Template, region);
        var others = MeanError(patch, other.Template, region);
        return own + others > 0 ? (others - own) / (own + others) : 0;
    }

    private static double MeanError(Mat patch, Mat template, Mat region)
    {
        using var error = new Mat();
        Cv2.Absdiff(patch, template, error);
        var mean = Cv2.Mean(error, region);
        return (mean.Val0 + mean.Val1 + mean.Val2) / 3;
    }

    private static Candidate BestMatch(Mat searchArea, string id, Mat template)
    {
        using var result = new Mat();
        Cv2.MatchTemplate(searchArea, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double max, out _, out Point location);
        return new Candidate(id, max, location, template);
    }

    /// <summary>A stratagem template at its best position in the search area.</summary>
    private sealed record Candidate(string Id, double Score, Point Location, Mat Template);

    public void Dispose()
    {
        foreach (var template in _templates.Values.Concat(_coarseTemplates.Values).Concat(_scaledTemplates.Values).SelectMany(t => t))
        {
            template.Dispose();
        }

        ClearCache();
    }

    private void ClearCache()
    {
        foreach (var key in _cache.Keys)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                entry.Canonical.Dispose();
            }
        }
    }

    private static double MeanAbsDiff(Mat a, Mat b)
    {
        using var diff = new Mat();
        Cv2.Absdiff(a, b, diff);
        var mean = Cv2.Mean(diff);
        return (mean.Val0 + mean.Val1 + mean.Val2) / 3;
    }

    private static Mat Half(Mat image)
    {
        var half = new Mat();
        Cv2.Resize(image, half, new Size(image.Width / 2, image.Height / 2), interpolation: InterpolationFlags.Area);
        return half;
    }

    private static double Match(Mat image, Mat template)
    {
        using var result = new Mat();
        Cv2.MatchTemplate(image, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double max);
        return max;
    }

    /// <summary>An empty slot is a uniform gray square; icons always add strong contrast.</summary>
    /// <param name="margin">Margin around the slot in the canonical image.</param>
    private static bool IsEmpty(Mat canonical, int margin)
    {
        var inner = new Rect(margin + 6, margin + 6, CanonicalSize - 12, CanonicalSize - 12);
        using var center = new Mat(canonical, inner);
        using var gray = new Mat();
        Cv2.CvtColor(center, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.MeanStdDev(gray, out _, out var stdDev);
        return stdDev.Val0 < 8;
    }
}
