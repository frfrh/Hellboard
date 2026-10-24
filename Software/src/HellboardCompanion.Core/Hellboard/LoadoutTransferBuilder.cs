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

    /// <summary>
    /// Before any loadout was locked: keeps what the board currently has (<paramref name="board"/>) and only
    /// applies the multi-function list of the settings, so a changed fixed list reaches the board at once.
    /// <paramref name="detected"/> are the mission stratagems detected in the last loadout of a previous session,
    /// so they stay on the board after a restart of the Companion. Without them (<c>null</c>) nothing detected is
    /// known: in <see cref="MultiFunctionMode.AutoDetect"/> the board's list then stays.
    /// </summary>
    public static LoadoutTransfer BuildMultiFunctionOnly(HellboardLoadout board, StratagemCatalog catalog, MultiFunctionOptions options, IReadOnlyList<string>? detected = null)
    {
        IEnumerable<string>? ids = options.Mode switch
        {
            MultiFunctionMode.FixedList => options.FixedList,
            MultiFunctionMode.AutoDetect => detected,
            _ => options.FixedList.Concat(detected ?? []),
        };
        if (ids is null)
        {
            return new LoadoutTransfer(board, []);
        }

        var notTransferable = new List<string>();
        var mission = new List<StratagemDefinition>();
        foreach (var id in ids.Distinct(StringComparer.Ordinal).Take(MaxMissionStratagems))
        {
            if (catalog.Find(id) is { IsTransferable: true } entry)
            {
                mission.Add(new StratagemDefinition(entry.Name, entry.Color, entry.Sequence!));
            }
            else
            {
                notTransferable.Add(id);
            }
        }

        return new LoadoutTransfer(board with { MissionStratagems = mission }, notTransferable);
    }
}
