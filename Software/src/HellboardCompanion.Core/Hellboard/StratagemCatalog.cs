// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;

namespace HellboardCompanion.Core.Hellboard;

/// <param name="Category">Category from <c>doc/stratagems.json</c>, e.g. "Orbital" or "Mission".</param>
/// <param name="Color">LED color of the category: yellow, red, green or blue.</param>
/// <param name="Sequence">Arrow sequence of U/D/L/R; <c>null</c> = not known yet (editor, implementation plan §6.12).</param>
public sealed record CatalogEntry(string Id, string Name, string Category, string Color, string? Sequence)
{
    public bool IsTransferable => !string.IsNullOrEmpty(Sequence);
}

/// <summary>
/// Stratagem names, colors and input sequences (implementation plan §6.11). Built from
/// <c>doc/stratagems.json</c> in the repository root – the single source of the stratagem codes of the project –
/// which is embedded into the assembly. Icons are handled separately by the icon store.
/// </summary>
public sealed partial class StratagemCatalog
{
    private const string ListResourceName = "HellboardCompanion.Core.Hellboard.stratagems.json";
    private const int SupportedVersion = 1;
    private const int MaxSequenceLength = 10;   // Hellboard protocol limit

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, CatalogEntry> _entries;

    public StratagemCatalog(IEnumerable<CatalogEntry> entries)
    {
        _entries = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<CatalogEntry> Entries => _entries.Values;

    /// <summary>The catalog shipped with the application (embedded <c>doc/stratagems.json</c>).</summary>
    public static StratagemCatalog LoadSeed()
    {
        using var stream = typeof(StratagemCatalog).Assembly.GetManifestResourceStream(ListResourceName)
            ?? throw new InvalidOperationException($"Embedded stratagem list '{ListResourceName}' not found.");
        return Parse(stream);
    }

    /// <summary>Reads and validates a stratagem list in the format of <c>doc/stratagems.json</c>.</summary>
    /// <exception cref="FormatException">The file is not valid JSON or breaks a rule of the format.</exception>
    public static StratagemCatalog Parse(Stream json)
    {
        StratagemList list;
        try
        {
            list = JsonSerializer.Deserialize<StratagemList>(json, JsonOptions)
                ?? throw new FormatException("Stratagem list is empty.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"Stratagem list is not valid JSON: {e.Message}", e);
        }

        if (list.Version != SupportedVersion)
        {
            throw new FormatException($"Unsupported stratagem list version {list.Version}.");
        }

        var categories = list.Categories ?? [];
        var entries = new List<CatalogEntry>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in list.Stratagems ?? [])
        {
            var id = item.Id ?? string.Empty;
            if (!IdPattern().IsMatch(id))
            {
                throw new FormatException($"Invalid id '{id}'.");
            }

            if (!ids.Add(id))
            {
                throw new FormatException($"Duplicate id '{id}'.");
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                throw new FormatException($"{id}: name missing.");
            }

            if (item.Category is null || !categories.TryGetValue(item.Category, out var color))
            {
                throw new FormatException($"{id}: unknown category '{item.Category}'.");
            }

            if (item.Code is { } code && (code.Length is 0 or > MaxSequenceLength || !CodePattern().IsMatch(code)))
            {
                throw new FormatException($"{id}: invalid code '{code}' (1–{MaxSequenceLength} of U, D, L, R).");
            }

            entries.Add(new CatalogEntry(id, item.Name, item.Category, color, item.Code));
        }

        return new StratagemCatalog(entries);
    }

    public CatalogEntry? Find(string id) => _entries.GetValueOrDefault(id);

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[UDLR]+$")]
    private static partial Regex CodePattern();

    private sealed record StratagemList(int Version, Dictionary<string, string>? Categories, List<StratagemItem>? Stratagems);

    /// <summary>One line of the list; <c>designation</c>, <c>firmwareMacro</c> and <c>notes</c> are not needed here.</summary>
    private sealed record StratagemItem(string? Id, string? Name, string? Category, string? Code);
}
