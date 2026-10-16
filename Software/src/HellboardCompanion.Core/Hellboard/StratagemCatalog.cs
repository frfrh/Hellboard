// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace HellboardCompanion.Core.Hellboard;

/// <param name="Color">LED color category: yellow, red, green, blue or white.</param>
/// <param name="Sequence">Arrow sequence of U/D/L/R; <c>null</c> = not known yet (editor, implementation plan §6.12).</param>
public sealed record CatalogEntry(string Id, string Name, string Group, string Color, string? Sequence)
{
    public bool IsTransferable => !string.IsNullOrEmpty(Sequence);
}

/// <summary>
/// Stratagem names, colors and input sequences (implementation plan §6.11). Seeded from
/// <c>assets/catalog.seed.json</c>; icons are handled separately by the icon store.
/// </summary>
public sealed class StratagemCatalog
{
    private const string SeedResourceName = "HellboardCompanion.Core.Hellboard.catalog.seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, CatalogEntry> _entries;

    public StratagemCatalog(IEnumerable<CatalogEntry> entries)
    {
        _entries = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<CatalogEntry> Entries => _entries.Values;

    /// <summary>The catalog shipped with the application.</summary>
    public static StratagemCatalog LoadSeed()
    {
        using var stream = typeof(StratagemCatalog).Assembly.GetManifestResourceStream(SeedResourceName)
            ?? throw new InvalidOperationException($"Embedded catalog '{SeedResourceName}' not found.");
        using var document = JsonDocument.Parse(stream);
        var entries = document.RootElement.GetProperty("stratagems").Deserialize<CatalogEntry[]>(JsonOptions)!;
        return new StratagemCatalog(entries);
    }

    public CatalogEntry? Find(string id) => _entries.GetValueOrDefault(id);
}
