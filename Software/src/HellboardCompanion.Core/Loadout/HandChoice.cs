// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Loadout;

/// <summary>
/// Choosing a player stratagem by hand in the overview (manual-selection plan §1–2): turns the shown
/// loadout with one slot changed into a complete <see cref="LockedLoadout"/>. The loadout keeps what the
/// recognition had in every slot (<see cref="LockedLoadout.Recognized"/>), so a slot can be reset to it (§1.3).
/// </summary>
public static class HandChoice
{
    /// <summary>Marks a slot in the shown loadout that is occupied but was not recognized.</summary>
    public const string NotRecognized = "?";

    /// <summary>
    /// Sets <paramref name="slot"/> to <paramref name="id"/> (<c>null</c> = empty). A stratagem that is
    /// already in another slot swaps places with the old content, as the game allows no duplicates; both
    /// slots then count as set by hand. A slot that ends up with what the recognition had there no longer
    /// counts as set by hand.
    /// </summary>
    /// <param name="shown">The shown player slots: stratagem id, <c>null</c> = empty, <see cref="NotRecognized"/>.</param>
    /// <param name="byHand">Hand marks of the shown loadout, <c>null</c> = none.</param>
    /// <param name="recognized">Recognized content of the shown loadout (<see cref="LockedLoadout.Recognized"/>), <c>null</c> = not known.</param>
    public static LockedLoadout Apply(
        IReadOnlyList<string?> shown,
        IReadOnlyList<bool>? byHand,
        IReadOnlyList<string> missionStratagems,
        int slot,
        string? id,
        DateTimeOffset timestamp,
        IReadOnlyList<string?>? recognized = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, shown.Count);

        var basis = Enumerable.Range(0, shown.Count).Select(i => IsMarked(byHand, i) ? RecognizedOrUnknown(recognized, i) : shown[i]).ToList();
        var slots = shown.ToList();
        var marks = Enumerable.Range(0, slots.Count).Select(i => IsMarked(byHand, i)).ToList();
        var other = id is null ? -1 : slots.IndexOf(id);
        if (other >= 0 && other != slot)
        {
            slots[other] = slots[slot];
            marks[other] = true;
        }

        slots[slot] = id;
        marks[slot] = true;
        for (var i = 0; i < marks.Count; i++)
        {
            marks[i] = marks[i] && (basis[i] == NotRecognized || slots[i] != basis[i]);
        }

        var anyByHand = marks.Contains(true);
        return new LockedLoadout(
            timestamp,
            slots.Select(s => s == NotRecognized ? null : s).ToList(),
            missionStratagems,
            LockSource.ByHand,
            !slots.Contains(NotRecognized),
            anyByHand ? marks : null,
            anyByHand ? basis : null);
    }

    /// <summary>
    /// Sets <paramref name="slot"/> back to what the recognition had there (right-click on a tile);
    /// <c>null</c> if the slot is not set by hand or its recognized content is not known.
    /// </summary>
    public static LockedLoadout? Reset(
        IReadOnlyList<string?> shown,
        IReadOnlyList<bool>? byHand,
        IReadOnlyList<string?>? recognized,
        IReadOnlyList<string> missionStratagems,
        int slot,
        DateTimeOffset timestamp) =>
        TryGetRecognized(byHand, recognized, slot, out var id)
            ? Apply(shown, byHand, missionStratagems, slot, id, timestamp, recognized)
            : null;

    /// <summary>What the recognition had in a slot set by hand (<c>null</c> = empty); <c>false</c> if the slot is not set by hand or that is not known.</summary>
    public static bool TryGetRecognized(IReadOnlyList<bool>? byHand, IReadOnlyList<string?>? recognized, int slot, out string? id)
    {
        id = RecognizedOrUnknown(recognized, slot);
        if (IsMarked(byHand, slot) && id != NotRecognized)
        {
            return true;
        }

        id = null;
        return false;
    }

    /// <summary>Player stratagems of the given loadouts (newest first), each once, without <paramref name="excluded"/>.</summary>
    public static IReadOnlyList<string> RecentlyUsed(IEnumerable<LockedLoadout> newestFirst, IReadOnlySet<string> excluded, int maxCount = 8) =>
        newestFirst
            .SelectMany(l => l.Stratagems)
            .OfType<string>()
            .Where(id => !excluded.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .Take(maxCount)
            .ToList();

    private static bool IsMarked(IReadOnlyList<bool>? byHand, int slot) => byHand is not null && slot < byHand.Count && byHand[slot];

    private static string? RecognizedOrUnknown(IReadOnlyList<string?>? recognized, int slot) =>
        recognized is not null && slot < recognized.Count ? recognized[slot] : NotRecognized;
}
