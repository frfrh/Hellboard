// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Recognition;

/// <summary>Content of one slot as used for comparisons (equality = same content).</summary>
public readonly record struct SlotValue(SlotState State, string? StratagemId)
{
    public static SlotValue Empty { get; } = new(SlotState.Empty, null);

    public static SlotValue From(SlotResult result) => result.State switch
    {
        SlotState.Recognized => new(SlotState.Recognized, result.StratagemId),
        SlotState.Empty => Empty,
        _ => new(SlotState.Unknown, null),
    };

    public override string ToString() => State switch
    {
        SlotState.Recognized => StratagemId!,
        SlotState.Empty => "empty",
        _ => "?",
    };
}

/// <summary>The loadout shown on the screen: 4 player stratagems and 4 optional mission stratagems.</summary>
public sealed record LoadoutSnapshot(IReadOnlyList<SlotValue> PlayerSlots, IReadOnlyList<SlotValue> MissionSlots)
{
    public bool Equals(LoadoutSnapshot? other) =>
        other is not null && PlayerSlots.SequenceEqual(other.PlayerSlots) && MissionSlots.SequenceEqual(other.MissionSlots);

    public override int GetHashCode() => HashCode.Combine(PlayerSlots.Count, PlayerSlots.FirstOrDefault(), MissionSlots.FirstOrDefault());

    public override string ToString() => $"{string.Join(", ", PlayerSlots)} | mission: {string.Join(", ", MissionSlots)}";
}

/// <summary>Result of analyzing one frame (implementation plan §6.4).</summary>
public sealed record LoadoutObservation(
    DateTimeOffset Timestamp,
    LoadoutScreenResult Screen,
    ReadyStatus Status,
    LoadoutSnapshot? Loadout,
    TimeSpan Duration)
{
    public bool ScreenVisible => Screen.IsVisible;
}

/// <summary>
/// Analyzes a frame of the game: loadout screen visible? ready status? which stratagems?
/// Stops after the cheap screen check when the loadout screen is not shown.
/// </summary>
public sealed class LoadoutRecognizer : IDisposable
{
    private readonly LoadoutLayout _layout;
    private readonly LoadoutScreenDetector _screenDetector;
    private readonly ReadyStatusDetector _readyDetector;
    private readonly StratagemSlotClassifier _playerClassifier;
    private readonly StratagemSlotClassifier _missionClassifier;

    /// <param name="missionOnly">Ids that only appear in mission slots (e.g. SEAF Artillery); they are
    /// not compared in the player slots, where some of them look almost like player stratagems.</param>
    /// <param name="references">Reference images assigned by the user (StratagemReferenceStore).</param>
    public LoadoutRecognizer(LoadoutLayout layout, IReadOnlyList<StratagemIcon> icons, IReadOnlySet<string>? missionOnly = null,
        IReadOnlyList<StratagemReference>? references = null)
    {
        _layout = layout;
        _screenDetector = new LoadoutScreenDetector(layout);
        _readyDetector = new ReadyStatusDetector(layout);
        _playerClassifier = new StratagemSlotClassifier(icons.Where(icon => missionOnly?.Contains(icon.Id) != true), SlotStyle.PlayerSlot,
            references: references?.Where(r => r.Slot == ReferenceSlot.Player && missionOnly?.Contains(r.Id) != true));
        _missionClassifier = new StratagemSlotClassifier(icons, SlotStyle.MissionSlot,
            references: references?.Where(r => r.Slot == ReferenceSlot.Mission));
    }

    /// <param name="frame">Capture of the game's client area.</param>
    /// <param name="force">Classify the slots at their layout positions even if the loadout screen is not
    /// detected (manual trigger and file analysis, implementation plan §6.13).</param>
    public LoadoutObservation Recognize(Frame frame, bool force = false)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var screen = _screenDetector.Detect(frame);
        if (!screen.IsVisible && !force)
        {
            return new LoadoutObservation(frame.Timestamp, screen, ReadyStatus.Unknown, null, System.Diagnostics.Stopwatch.GetElapsedTime(started));
        }

        var status = _readyDetector.Detect(frame, screen.Offset).Status;
        var scaler = new LayoutScaler(_layout, new PixelRect(0, 0, frame.Width, frame.Height));
        PixelRect Locate(LayoutRect slot)
        {
            var rect = scaler.ToScreen(slot);
            return rect with { X = rect.X + screen.Offset.X, Y = rect.Y + screen.Offset.Y };
        }

        // The slots are independent; classify them in parallel (templates are only read).
        var jobs = _layout.PlayerSlots.Select(s => (Classifier: _playerClassifier, Rect: Locate(s)))
            .Concat(_layout.OptionalMissionSlots.Select(s => (Classifier: _missionClassifier, Rect: Locate(s))))
            .ToArray();
        var values = new SlotValue[jobs.Length];
        Parallel.For(0, jobs.Length, i => values[i] = SlotValue.From(jobs[i].Classifier.Classify(frame, jobs[i].Rect)));
        var player = values[.._layout.PlayerSlots.Count];
        var mission = values[_layout.PlayerSlots.Count..];

        return new LoadoutObservation(frame.Timestamp, screen, status, new LoadoutSnapshot(player, mission), System.Diagnostics.Stopwatch.GetElapsedTime(started));
    }

    /// <summary>Position of a slot in <paramref name="frame"/>, corrected by the offset of the screen detection.</summary>
    /// <param name="mission">true = optional mission slot, false = player slot.</param>
    public PixelRect GetSlotRect(Frame frame, PixelOffset offset, bool mission, int index)
    {
        var scaler = new LayoutScaler(_layout, new PixelRect(0, 0, frame.Width, frame.Height));
        var rect = scaler.ToScreen(mission ? _layout.OptionalMissionSlots[index] : _layout.PlayerSlots[index]);
        return rect with { X = rect.X + offset.X, Y = rect.Y + offset.Y };
    }

    public void Dispose()
    {
        _playerClassifier.Dispose();
        _missionClassifier.Dispose();
    }
}
