// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json.Serialization;
using HellboardCompanion.Core.Recognition;

namespace HellboardCompanion.Core.Loadout;

public enum TrackerState
{
    /// <summary>No loadout screen.</summary>
    Idle,

    /// <summary>Loadout screen visible, player is selecting (Prepare).</summary>
    Selecting,

    /// <summary>Player confirmed (Ready); the loadout is locked in.</summary>
    Locked,
}

public enum LockSource
{
    /// <summary>The ready button changed to Ready.</summary>
    Ready,

    /// <summary>The loadout screen disappeared before Ready was seen (e.g. last player confirmed).</summary>
    ScreenExit,

    /// <summary>Manual trigger (implementation plan §6.13).</summary>
    Manual,

    /// <summary>Stratagems chosen by hand in the overview (manual-selection plan).</summary>
    ByHand,
}

/// <param name="Stratagems">Player stratagem ids in slot order; <c>null</c> = empty or not recognized.</param>
/// <param name="MissionStratagems">Recognized optional mission stratagems in slot order.</param>
/// <param name="IsComplete">No player slot was left unrecognized.</param>
/// <param name="ByHand">Per player slot: set by hand; <c>null</c> = no slot was set by hand.</param>
public sealed record LockedLoadout(
    DateTimeOffset Timestamp,
    IReadOnlyList<string?> Stratagems,
    IReadOnlyList<string> MissionStratagems,
    LockSource Source,
    bool IsComplete,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<bool>? ByHand = null)
{
    public static LockedLoadout From(LoadoutSnapshot snapshot, DateTimeOffset timestamp, LockSource source) => new(
        timestamp,
        snapshot.PlayerSlots.Select(s => s.State == SlotState.Recognized ? s.StratagemId : null).ToList(),
        snapshot.MissionSlots.Where(s => s.State == SlotState.Recognized).Select(s => s.StratagemId!).ToList(),
        source,
        snapshot.PlayerSlots.All(s => s.State != SlotState.Unknown));

    public bool IsSetByHand(int slot) => ByHand is { } byHand && slot < byHand.Count && byHand[slot];

    /// <summary>Same stratagems (ignores time, source and hand marks).</summary>
    public bool HasSameContent(LockedLoadout? other) =>
        other is not null && Stratagems.SequenceEqual(other.Stratagems) && MissionStratagems.SequenceEqual(other.MissionStratagems);

    public override string ToString() =>
        $"{string.Join(", ", Stratagems.Select(s => s ?? "-"))} | mission: {string.Join(", ", MissionStratagems)} ({Source}{(IsComplete ? string.Empty : ", incomplete")})";
}

/// <summary>
/// Lock-in logic of implementation plan §6.5. Keeps a stable candidate while the player is
/// selecting, locks it in when the ready button turns to Ready (or the screen closes before),
/// and starts over when the player withdraws the ready state. A loadout chosen by hand stays
/// until the game shows something different (manual-selection plan §3.2). Not thread-safe; feed
/// it from one thread.
/// </summary>
public sealed class LoadoutTracker(int stableFrames = 3, int statusFrames = 2, int exitFrames = 2)
{
    private LoadoutSnapshot? _lastSnapshot;
    private int _snapshotRepeats;
    private ReadyStatus _lastStatus = ReadyStatus.Unknown;
    private int _statusRepeats;
    private int _missingFrames;

    /// <summary>The automatic content the active hand choice replaced; <c>null</c> = none or nothing was recognized.</summary>
    private LockedLoadout? _correctedFrom;

    public TrackerState State { get; private set; } = TrackerState.Idle;

    /// <summary>Loadout recognized identically in the last <c>stableFrames</c> frames.</summary>
    public LoadoutSnapshot? Candidate { get; private set; }

    public LockedLoadout? LastLocked { get; private set; }

    /// <summary>What the recognition showed before the active hand choice; <c>null</c> = no hand choice or nothing recognized.</summary>
    public LockedLoadout? CorrectedContent => _correctedFrom;

    /// <summary>Last loadout locked in by the recognition (Ready, screen closed or manual trigger).</summary>
    public LockedLoadout? LastRecognized { get; private set; }

    public event EventHandler<LockedLoadout>? Locked;

    public void Process(LoadoutObservation observation)
    {
        if (!observation.ScreenVisible || observation.Loadout is null)
        {
            HandleMissingScreen(observation.Timestamp);
            return;
        }

        _missingFrames = 0;
        if (State == TrackerState.Idle)
        {
            State = TrackerState.Selecting;
        }

        UpdateCandidate(observation.Loadout);
        var status = UpdateStatus(observation.Status);

        if (status == ReadyStatus.Ready && State == TrackerState.Selecting && Candidate is not null)
        {
            Lock(Candidate, observation.Timestamp, LockSource.Ready);
            State = TrackerState.Locked;
        }
        else if (status == ReadyStatus.Prepare && State == TrackerState.Locked)
        {
            // Ready withdrawn (multiplayer): the player may change the loadout again.
            State = TrackerState.Selecting;
            ResetCandidate();
        }
    }

    /// <summary>Locks in a loadout directly, e.g. from the manual trigger.</summary>
    public void LockManually(LoadoutSnapshot snapshot, DateTimeOffset timestamp) => Lock(snapshot, timestamp, LockSource.Manual);

    /// <summary>
    /// Locks in a loadout chosen by hand. An automatic lock-in with the content the hand choice
    /// replaced is skipped afterwards; any other content replaces the hand choice.
    /// </summary>
    public void LockByHand(LockedLoadout loadout)
    {
        if (State == TrackerState.Selecting && Candidate is { } candidate)
        {
            // Corrected on the loadout screen: Ready would lock in this candidate.
            var live = LockedLoadout.From(candidate, loadout.Timestamp, LockSource.Ready);
            if (!live.HasSameContent(_correctedFrom))
            {
                _correctedFrom = live;
            }
        }
        else if (LastLocked?.Source != LockSource.ByHand)
        {
            _correctedFrom = LastLocked;
        }

        var locked = loadout with { Source = LockSource.ByHand };
        LastLocked = locked;
        Locked?.Invoke(this, locked);
    }

    /// <summary>Drops the hand choice and locks the last recognized loadout in again; <c>false</c> if there is none.</summary>
    public bool RevertToRecognized()
    {
        if (LastLocked?.Source != LockSource.ByHand || LastRecognized is not { } recognized)
        {
            return false;
        }

        _correctedFrom = null;
        LastLocked = recognized;
        Locked?.Invoke(this, recognized);
        return true;
    }

    /// <summary>The live loadout is still the one a hand choice corrected, so the hand choice is what counts.</summary>
    public bool IsCorrectedByHand(LoadoutSnapshot live) =>
        _correctedFrom is { } corrected && LockedLoadout.From(live, corrected.Timestamp, corrected.Source).HasSameContent(corrected);

    private void HandleMissingScreen(DateTimeOffset timestamp)
    {
        if (State == TrackerState.Idle || ++_missingFrames < exitFrames)
        {
            return;
        }

        if (State == TrackerState.Selecting && Candidate is not null)
        {
            Lock(Candidate, timestamp, LockSource.ScreenExit);
        }

        State = TrackerState.Idle;
        ResetCandidate();
        _lastStatus = ReadyStatus.Unknown;
        _statusRepeats = 0;
    }

    private void UpdateCandidate(LoadoutSnapshot snapshot)
    {
        _snapshotRepeats = snapshot.Equals(_lastSnapshot) ? _snapshotRepeats + 1 : 1;
        _lastSnapshot = snapshot;
        if (_snapshotRepeats >= stableFrames && State != TrackerState.Locked)
        {
            Candidate = snapshot;
        }
    }

    /// <summary>Returns the debounced status; <see cref="ReadyStatus.Unknown"/> frames are ignored.</summary>
    private ReadyStatus UpdateStatus(ReadyStatus status)
    {
        if (status == ReadyStatus.Unknown)
        {
            return ReadyStatus.Unknown;
        }

        _statusRepeats = status == _lastStatus ? _statusRepeats + 1 : 1;
        _lastStatus = status;
        return _statusRepeats >= statusFrames ? status : ReadyStatus.Unknown;
    }

    private void ResetCandidate()
    {
        Candidate = null;
        _lastSnapshot = null;
        _snapshotRepeats = 0;
    }

    private void Lock(LoadoutSnapshot snapshot, DateTimeOffset timestamp, LockSource source)
    {
        var locked = LockedLoadout.From(snapshot, timestamp, source);
        LastRecognized = locked;
        if (source != LockSource.Manual && locked.HasSameContent(_correctedFrom))
        {
            // The game still shows what the player corrected by hand.
            return;
        }

        _correctedFrom = null;
        LastLocked = locked;
        Locked?.Invoke(this, locked);
    }
}
