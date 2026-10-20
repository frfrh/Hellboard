// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Loadout;

/// <summary>
/// Choosing a player stratagem by hand in the overview (manual-selection plan §1–2): turns the shown
/// loadout with one slot changed into a complete <see cref="LockedLoadout"/>.
/// </summary>
public static class HandChoice
{
    /// <summary>Marks a slot in the shown loadout that is occupied but was not recognized.</summary>
    public const string NotRecognized = "?";

    /// <summary>
    /// Sets <paramref name="slot"/> to <paramref name="id"/> (<c>null</c> = empty). A stratagem that is
    /// already in another slot swaps places with the old content, as the game allows no duplicates; both
    /// slots then count as set by hand.
    /// </summary>
    /// <param name="shown">The shown player slots: stratagem id, <c>null</c> = empty, <see cref="NotRecognized"/>.</param>
    /// <param name="byHand">Hand marks of the shown loadout, <c>null</c> = none.</param>
    public static LockedLoadout Apply(
        IReadOnlyList<string?> shown,
        IReadOnlyList<bool>? byHand,
        IReadOnlyList<string> missionStratagems,
        int slot,
        string? id,
        DateTimeOffset timestamp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, shown.Count);

        var slots = shown.ToList();
        var marks = Enumerable.Range(0, slots.Count).Select(i => byHand is not null && i < byHand.Count && byHand[i]).ToList();
        var other = id is null ? -1 : slots.IndexOf(id);
        if (other >= 0 && other != slot)
        {
            slots[other] = slots[slot];
            marks[other] = true;
        }

        slots[slot] = id;
        marks[slot] = true;
        return new LockedLoadout(
            timestamp,
            slots.Select(s => s == NotRecognized ? null : s).ToList(),
            missionStratagems,
            LockSource.ByHand,
            !slots.Contains(NotRecognized),
            marks);
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
}
