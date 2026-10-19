// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace HellboardCompanion.Core.Hellboard;

/// <param name="Name">Short name: sent to the Hellboard and used for sorting (wiki-source-plan §3.3).</param>
/// <param name="Category">Category from <c>doc/stratagems.json</c>, e.g. "Orbital" or "Mission".</param>
/// <param name="Color">LED color of the category: yellow, red, green or blue.</param>
/// <param name="Sequence">Arrow sequence of U/D/L/R; <c>null</c> = not known yet (editor, implementation plan §6.12).</param>
/// <param name="Designation">Full name = page name in the Helldivers wiki, e.g. "B/FLAM-80 Cremator"; <c>null</c> if unknown.</param>
public sealed record CatalogEntry(string Id, string Name, string Category, string Color, string? Sequence, string? Designation = null)
{
    public bool IsTransferable => !string.IsNullOrEmpty(Sequence);

    /// <summary>Name shown in the Companion: the full designation, the short name if there is none.</summary>
    public string FullName => Designation ?? Name;
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


    private volatile Dictionary<string, CatalogEntry> _entries;
    private volatile IReadOnlySet<string> _userIds = new HashSet<string>();
    private volatile IReadOnlyDictionary<string, string> _categories;

    public StratagemCatalog(IEnumerable<CatalogEntry> entries)
    {
        _entries = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
        _categories = _entries.Values.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.First().Color, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<CatalogEntry> Entries => _entries.Values;

    /// <summary>Ids of the entries that come from the user's own list (added or replacing a built-in one).</summary>
    public IReadOnlySet<string> UserIds => _userIds;

    /// <summary>All categories with their LED color (built-in and user).</summary>
    public IReadOnlyDictionary<string, string> Categories => _categories;

    /// <summary>Ids of the category "Mission": only in mission slots, never selectable as player stratagem.</summary>
    public IReadOnlySet<string> MissionOnlyIds() =>
        _entries.Values.Where(e => e.Category == "Mission").Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>The user's own list this catalog was loaded with (<see cref="Load"/>), <c>null</c> otherwise.</summary>
    public string? UserFilePath { get; private set; }

    /// <summary>Raised after <see cref="ReplaceWith"/>, e.g. when the stratagem editor saved a change.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Takes over the content of <paramref name="other"/> (e.g. after the user's list was edited), so all
    /// components that hold this catalog see the change without a restart.
    /// </summary>
    public void ReplaceWith(StratagemCatalog other)
    {
        _entries = other._entries;
        _userIds = other._userIds;
        _categories = other._categories;
        UserFilePath = other.UserFilePath ?? UserFilePath;
        Changed?.Invoke(this, EventArgs.Empty);
    }

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
                UserStratagemList.Load(userFile).Save();   // empty template with a description of the format
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not create the user stratagem list {File}", userFile);
            }

            var seed = LoadSeed();
            seed.UserFilePath = userFile;
            return seed;
        }

        try
        {
            using var builtIn = OpenBuiltIn();
            using var user = File.OpenRead(userFile);
            var catalog = Parse(builtIn, user);
            catalog.UserFilePath = userFile;
            if (catalog.UserIds.Count > 0)
            {
                logger.LogInformation("User stratagem list {File}: {Ids}", userFile, string.Join(", ", catalog.UserIds.Order()));
            }

            return catalog;
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("User stratagem list {File} ignored, using the built-in list: {Error}", userFile, ex.Message);
            var seed = LoadSeed();
            seed.UserFilePath = userFile;
            return seed;
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
                    // The user list has no designation: a corrected code or name keeps the full name of the built-in entry.
                    items[index] = item with { Designation = item.Designation ?? items[index].Designation };
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

        var entries = items.Select(i => new CatalogEntry(i.Id!, i.Name!, i.Category!, categories[i.Category!], i.Code, i.Designation));
        var catalog = new StratagemCatalog(entries);
        catalog._userIds = userIds;
        catalog._categories = categories;
        return catalog;
    }

    public CatalogEntry? Find(string id) => _entries.GetValueOrDefault(id);

    /// <summary>
    /// Short name of a stratagem: its full name without the model prefix, removed once
    /// ("B/FLAM-80 Cremator" → "Cremator", "GL-28 Belt-Fed Grenade Launcher" → "Belt-Fed Grenade Launcher").
    /// Hardly any player knows a stratagem by its model (wiki-source-plan §3.3).
    /// </summary>
    public static string ShortName(string fullName) => ModelPrefix().Replace(fullName, string.Empty, 1);

    /// <summary>
    /// The catalog in the format of <c>doc/stratagems.json</c> with one stratagem per line, as the firmware
    /// reads it on the board (F11); includes the user's own entries.
    /// </summary>
    public string ToBoardJson()
    {
        var categories = new JsonObject();
        foreach (var group in _entries.Values.GroupBy(e => e.Category).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            categories[group.Key] = group.First().Color;
        }

        var lines = _entries.Values
            .OrderBy(e => e.Category, StringComparer.Ordinal)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => "    " + new JsonObject { ["id"] = e.Id, ["name"] = e.Name, ["category"] = e.Category, ["code"] = e.Sequence }.ToJsonString());
        return "{\n  \"version\": 1,\n  \"categories\": " + categories.ToJsonString() + ",\n  \"stratagems\": [\n"
            + string.Join(",\n", lines) + "\n  ]\n}\n";
    }

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

    /// <summary>Model prefix of a full name, e.g. "B/FLAM-80 " or "AX/LAS-5 " (user rule, 2026-10-19).</summary>
    [GeneratedRegex(@"^([\p{L}\p{N}/\\]{1,10}-[\p{L}\p{N}]{1,10}) ")]
    private static partial Regex ModelPrefix();

    private sealed record StratagemList(int Version, Dictionary<string, string>? Categories, List<StratagemItem>? Stratagems);

    /// <summary>One line of the list; <c>notes</c> are not needed here.</summary>
    private sealed record StratagemItem(string? Id, string? Name, string? Category, string? Code, string? Designation = null);
}
