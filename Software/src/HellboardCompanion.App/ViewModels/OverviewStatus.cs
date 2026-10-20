// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Hellboard.Update;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.App.ViewModels;

/// <summary>Color of the dot in front of a status entry.</summary>
public enum StatusLevel
{
    Inactive,
    Good,
    Attention,
    Problem,
}

/// <summary>
/// One entry of the status chain of the overview: a short text that always fits on one line and an
/// optional, smaller detail line (UI polish plan §2).
/// </summary>
public sealed record StatusLine(string Text, string? Detail, StatusLevel Level)
{
    /// <summary>Longest short text, so an entry never needs to wrap its first line.</summary>
    public const int MaxTextLength = 28;

    /// <summary>Short text and detail, e.g. for the tooltip.</summary>
    public string FullText => Detail is null ? Text : $"{Text}{Environment.NewLine}{Detail}";
}

/// <summary>One framed group of the multi-function list ("Fixed list" or "Detected").</summary>
public sealed record MultiFunctionGroupViewModel(string Title, IReadOnlyList<StratagemTileViewModel> Tiles);

/// <summary>Origin of the entries of the multi-function list (UI polish plan §3).</summary>
public static class MultiFunctionGroups
{
    public const string FixedTitle = "Fixed list";
    public const string DetectedTitle = "Detected";

    /// <summary>
    /// Splits the list as it is on the board (fixed entries first, duplicates already removed by
    /// LoadoutTransferBuilder) into its groups; empty groups are left out. An entry that is in the fixed
    /// list counts as fixed, also when it was detected as well.
    /// </summary>
    public static IReadOnlyList<(string Title, IReadOnlyList<T> Entries)> Split<T>(IEnumerable<T> entries, Func<T, string?> id, MultiFunctionOptions options)
    {
        var fixedIds = options.Mode == MultiFunctionMode.AutoDetect ? [] : options.FixedList.ToHashSet(StringComparer.Ordinal);
        var list = entries.ToList();
        var groups = new List<(string, IReadOnlyList<T>)>();
        var fixedEntries = list.Where(e => id(e) is { } i && fixedIds.Contains(i)).ToList();
        var detected = list.Except(fixedEntries).ToList();
        if (fixedEntries.Count > 0)
        {
            groups.Add((FixedTitle, fixedEntries));
        }

        if (detected.Count > 0)
        {
            groups.Add((DetectedTitle, detected));
        }

        return groups;
    }
}

/// <summary>Texts of the status chain game → recognition → Hellboard.</summary>
public static class OverviewStatus
{
    public static StatusLine Game(GameSnapshot snapshot) => snapshot switch
    {
        { Status: GameStatus.Running, Location.IsMinimized: true } => new("Helldivers 2 minimized", null, StatusLevel.Attention),
        { Status: GameStatus.Running, Location: { } location } => new(
            "Helldivers 2 running",
            $"{location.Monitor.DeviceName.TrimStart('\\', '.')} · {location.ClientBounds.Width}×{location.ClientBounds.Height}",
            StatusLevel.Good),
        { Status: GameStatus.Starting } => new("Helldivers 2 starting …", null, StatusLevel.Attention),
        _ => new("Helldivers 2 not running", null, StatusLevel.Inactive),
    };

    /// <param name="hasObservation">The recognition has looked at the game at least once.</param>
    public static StatusLine Recognition(TrackerState? state, LockSource? lastLock, bool hasObservation) => state switch
    {
        TrackerState.Selecting => new("Loadout screen", "selecting", StatusLevel.Attention),
        _ when lastLock == LockSource.ByHand => new("Set by hand", "click a tile to change", StatusLevel.Good),
        TrackerState.Locked => new("Loadout confirmed", Describe(lastLock ?? LockSource.Ready), StatusLevel.Good),
        _ when !hasObservation => new("Waiting", "for the game", StatusLevel.Inactive),
        _ => new("Waiting", "for the loadout screen", StatusLevel.Inactive),
    };

    /// <param name="bundled">Firmware version the Companion can install.</param>
    public static StatusLine Hellboard(HellboardState? state, string? bundled)
    {
        if (state is null)
        {
            return new("No Hellboard connected", null, StatusLevel.Inactive);
        }

        var firmware = FirmwareVersions.Classify(state, bundled);
        return state.Status switch
        {
            HellboardStatus.Connected when state.Port == "Simulator" => new("Hellboard simulator", null, StatusLevel.Attention),
            HellboardStatus.Connected when firmware == FirmwareStatus.Outdated => new(
                "Firmware outdated",
                $"{state.Port} · {state.Info?.Firmware} → {bundled}, update on the Hellboard tab",
                StatusLevel.Attention),
            HellboardStatus.Connected => new("Hellboard connected", state.Port, StatusLevel.Good),
            HellboardStatus.NoProtocol when firmware != FirmwareStatus.Incompatible => new(   // e.g. the port is used by another program
                "Hellboard not reachable",
                state.Message is { Length: > 0 } message ? $"{state.Port} · {message}" : state.Port,
                StatusLevel.Attention),
            HellboardStatus.NoProtocol when state.Info?.Protocol > HellboardClient.ProtocolVersion => new(
                "Companion too old",
                "the Hellboard firmware is newer – update Hellboard Companion",
                StatusLevel.Problem),
            HellboardStatus.NoProtocol => new("Firmware not compatible", "install the firmware on the Hellboard tab", StatusLevel.Problem),
            HellboardStatus.Updating => new("Installing firmware …", null, StatusLevel.Attention),
            HellboardStatus.DryRun => new("Hellboard dry run", "nothing is sent", StatusLevel.Attention),
            _ => new("No Hellboard connected", null, StatusLevel.Inactive),
        };
    }

    /// <summary>Title above the tiles for a locked loadout, e.g. "Locked in at 14:03:12 (Ready), slot 2 set by hand".</summary>
    /// <param name="recognized">The loadout the recognition locked in last, if any.</param>
    public static string LockedTitle(LockedLoadout locked, LockedLoadout? recognized)
    {
        var time = locked.Timestamp.LocalDateTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        if (locked.Source != LockSource.ByHand)
        {
            return $"Locked in at {time} ({Describe(locked.Source)})";
        }

        var slots = Enumerable.Range(1, locked.Stratagems.Count).Where(n => locked.IsSetByHand(n - 1)).ToList();
        if (recognized is null || slots.Count == 0 || slots.Count == locked.Stratagems.Count)
        {
            return $"Set by hand at {time}";
        }

        var recognizedTime = recognized.Timestamp.LocalDateTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var list = slots.Count == 1
            ? $"slot {slots[0]}"
            : $"slots {string.Join(", ", slots.SkipLast(1))} and {slots[^1]}";
        return $"Locked in at {recognizedTime} ({Describe(recognized.Source)}), {list} set by hand";
    }

    public static string Describe(LockSource source) => source switch
    {
        LockSource.Ready => "Ready",
        LockSource.ScreenExit => "screen closed",
        LockSource.ByHand => "set by hand",
        _ => "manual",
    };
}
