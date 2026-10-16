// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Loadout;

namespace HellboardCompanion.Core.Hellboard;

public enum MultiFunctionMode
{
    /// <summary>Only the stratagems selected by the user.</summary>
    FixedList,

    /// <summary>Only the optional mission stratagems recognized on the loadout screen.</summary>
    AutoDetect,

    /// <summary>Fixed list first, then the recognized ones; duplicates removed.</summary>
    FixedListAndAutoDetect,
}

/// <summary>Content of the multi-function button (implementation plan §6.6).</summary>
public sealed class MultiFunctionOptions
{
    public MultiFunctionMode Mode { get; set; } = MultiFunctionMode.FixedListAndAutoDetect;

    /// <summary>Catalog ids in button order. Default matches the former hard-coded firmware list.</summary>
    public List<string> FixedList { get; set; } = ["sos_beacon"];
}

/// <param name="NotTransferable">Ids that could not be sent (unknown or without input sequence).</param>
public sealed record LoadoutTransfer(HellboardLoadout Loadout, IReadOnlyList<string> NotTransferable);

/// <summary>Turns a locked loadout into the <c>setLoadout</c> content (hellboard-protocol.md §4.2).</summary>
public static class LoadoutTransferBuilder
{
    public const int MaxMissionStratagems = 16;

    public static LoadoutTransfer Build(LockedLoadout locked, StratagemCatalog catalog, MultiFunctionOptions options)
    {
        var notTransferable = new List<string>();

        StratagemDefinition? Define(string id)
        {
            if (catalog.Find(id) is { IsTransferable: true } entry)
            {
                return new StratagemDefinition(entry.Name, entry.Color, entry.Sequence!);   // full name, never shortened
            }

            notTransferable.Add(id);
            return null;
        }

        var stratagems = locked.Stratagems.Select(id => id is null ? null : Define(id)).ToList();

        IEnumerable<string> multiFunctionIds = options.Mode switch
        {
            MultiFunctionMode.FixedList => options.FixedList,
            MultiFunctionMode.AutoDetect => locked.MissionStratagems,
            _ => options.FixedList.Concat(locked.MissionStratagems),
        };

        var mission = multiFunctionIds
            .Distinct(StringComparer.Ordinal)
            .Select(Define)
            .OfType<StratagemDefinition>()
            .Take(MaxMissionStratagems)
            .ToList();

        return new LoadoutTransfer(new HellboardLoadout(stratagems, mission), notTransferable);
    }
}
