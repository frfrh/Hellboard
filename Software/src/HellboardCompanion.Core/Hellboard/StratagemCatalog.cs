// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Microsoft.Extensions.Logging;
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

    private static readonly HashSet<string> Colors = new(StringComparer.Ordinal) { "red", "green", "blue", "yellow", "white" };

    private const string UserFileTemplate = """
        {
          "$comment": "Your own stratagems and corrections for Hellboard Companion. Same format as doc/stratagems.json of the Hellboard project (described in doc/stratagems.md): an entry with the id of a built-in stratagem replaces it, a new id adds a stratagem. Codes use U, D, L, R. Categories: Orbital, Eagle, Support weapon, Backpack, Vehicle, Defensive, Mission. Restart the Companion after a change. Example entry: {\"id\": \"my_stratagem\", \"name\": \"My Stratagem\", \"category\": \"Mission\", \"code\": \"UDLR\"}",
          "version": 1,
          "stratagems": [
          ]
        }
        """;

    private readonly Dictionary<string, CatalogEntry> _entries;

    public StratagemCatalog(IEnumerable<CatalogEntry> entries)
    {
        _entries = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<CatalogEntry> Entries => _entries.Values;

    /// <summary>Ids of the entries that come from the user's own list (added or replacing a built-in one).</summary>
    public IReadOnlySet<string> UserIds { get; private init; } = new HashSet<string>();

    /// <summary>The catalog shipped with the application (embedded <c>doc/stratagems.json</c>).</summary>
    public static StratagemCatalog LoadSeed()
    {
        using var stream = OpenBuiltIn();
        return Parse(stream);
    }

    /// <summary>
    /// Built-in list plus the user's own list <paramref name="userFile"/> (same format): an entry with the
    /// id of a built-in stratagem replaces it, a new id adds one, user categories add to the built-in ones.
    /// A missing user file is created as an empty template; an invalid one is ignored with a warning.
    /// </summary>
    public static StratagemCatalog Load(string userFile, ILogger logger)
    {
        if (!File.Exists(userFile))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(userFile)!);
                File.WriteAllText(userFile, UserFileTemplate.ReplaceLineEndings());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not create the user stratagem list {File}", userFile);
            }

            return LoadSeed();
        }

        try
        {
            using var builtIn = OpenBuiltIn();
            using var user = File.OpenRead(userFile);
            var catalog = Parse(builtIn, user);
            if (catalog.UserIds.Count > 0)
            {
                logger.LogInformation("User stratagem list {File}: {Ids}", userFile, string.Join(", ", catalog.UserIds.Order()));
            }

            return catalog;
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("User stratagem list {File} ignored, using the built-in list: {Error}", userFile, ex.Message);
            return LoadSeed();
        }
    }

    /// <summary>Reads and validates a stratagem list in the format of <c>doc/stratagems.json</c>, optionally with user overrides.</summary>
    /// <exception cref="FormatException">A list is not valid JSON or breaks a rule of the format.</exception>
    public static StratagemCatalog Parse(Stream json, Stream? overrides = null)
    {
        var list = Read(json);
        var categories = new Dictionary<string, string>(list.Categories ?? [], StringComparer.Ordinal);
        var items = Validate(list.Stratagems ?? [], categories, "built-in");
        var userIds = new HashSet<string>(StringComparer.Ordinal);

        if (overrides is not null)
        {
            var user = Read(overrides);
            foreach (var (name, color) in user.Categories ?? [])
            {
                categories[name] = color;
            }

            foreach (var item in Validate(user.Stratagems ?? [], categories, "user"))
            {
                var index = items.FindIndex(i => i.Id == item.Id);
                if (index >= 0)
                {
                    items[index] = item;
                }
                else
                {
                    items.Add(item);
                }

                userIds.Add(item.Id!);
            }
        }

        foreach (var (name, color) in categories)
        {
            if (!Colors.Contains(color))
            {
                throw new FormatException($"Category '{name}': unknown color '{color}'.");
            }
        }

        var entries = items.Select(i => new CatalogEntry(i.Id!, i.Name!, i.Category!, categories[i.Category!], i.Code));
        return new StratagemCatalog(entries) { UserIds = userIds };
    }

    public CatalogEntry? Find(string id) => _entries.GetValueOrDefault(id);

    private static Stream OpenBuiltIn() =>
        typeof(StratagemCatalog).Assembly.GetManifestResourceStream(ListResourceName)
            ?? throw new InvalidOperationException($"Embedded stratagem list '{ListResourceName}' not found.");

    private static StratagemList Read(Stream json)
    {
        StratagemList? list;
        try
        {
            list = JsonSerializer.Deserialize<StratagemList>(json, JsonOptions);
        }
        catch (JsonException e)
        {
            throw new FormatException($"Stratagem list is not valid JSON: {e.Message}", e);
        }

        if (list is null)
        {
            throw new FormatException("Stratagem list is empty.");
        }

        return list.Version == SupportedVersion ? list : throw new FormatException($"Unsupported stratagem list version {list.Version}.");
    }

    private static List<StratagemItem> Validate(List<StratagemItem> items, Dictionary<string, string> categories, string source)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var id = item.Id ?? string.Empty;
            if (!IdPattern().IsMatch(id))
            {
                throw new FormatException($"{source} list: invalid id '{id}'.");
            }

            if (!ids.Add(id))
            {
                throw new FormatException($"{source} list: duplicate id '{id}'.");
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                throw new FormatException($"{source} list: {id}: name missing.");
            }

            if (item.Category is null || !categories.ContainsKey(item.Category))
            {
                throw new FormatException($"{source} list: {id}: unknown category '{item.Category}'.");
            }

            if (item.Code is { } code && (code.Length is 0 or > MaxSequenceLength || !CodePattern().IsMatch(code)))
            {
                throw new FormatException($"{source} list: {id}: invalid code '{code}' (1–{MaxSequenceLength} of U, D, L, R).");
            }
        }

        return [.. items];
    }

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[UDLR]+$")]
    private static partial Regex CodePattern();

    private sealed record StratagemList(int Version, Dictionary<string, string>? Categories, List<StratagemItem>? Stratagems);

    /// <summary>One line of the list; <c>designation</c> and <c>notes</c> are not needed here.</summary>
    private sealed record StratagemItem(string? Id, string? Name, string? Category, string? Code);
}
