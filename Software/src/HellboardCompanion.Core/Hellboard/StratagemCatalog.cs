// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;
using HellboardCompanion.Core.Catalog;
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

/// <summary>The wiki lists another code for a stratagem than the catalog uses (wiki-source-plan §3.2).</summary>
/// <param name="Code">Code the catalog uses now (built-in or the user's), <c>null</c> if none.</param>
/// <param name="WikiCode">First code the wiki lists.</param>
public sealed record WikiProposal(string Id, string? Code, string WikiCode);

/// <summary>
/// Stratagem names, colors and input sequences (implementation plan §6.11). Three layers: the built-in
/// <c>doc/stratagems.json</c> (embedded), the stratagem table of helldivers.wiki.gg (wiki-source-plan §3.2:
/// adds new stratagems, fills missing codes, keeps names and designations current, but only *proposes* other
/// codes) and the user's own list, which always wins. Icons are handled separately by the icon store.
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
    private volatile IReadOnlySet<string> _wikiIds = new HashSet<string>();
    private volatile IReadOnlyList<WikiProposal> _wikiProposals = [];
    private volatile IReadOnlyList<WikiStratagem> _wiki = [];
    private volatile IReadOnlyDictionary<string, string> _notes = new Dictionary<string, string>();
    private volatile IReadOnlyDictionary<string, string> _categories;

    public StratagemCatalog(IEnumerable<CatalogEntry> entries)
    {
        _entries = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
        _categories = _entries.Values.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.First().Color, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<CatalogEntry> Entries => _entries.Values;

    /// <summary>Ids of the entries that come from the user's own list (added or replacing a built-in one).</summary>
    public IReadOnlySet<string> UserIds => _userIds;

    /// <summary>Ids of the entries the wiki added or whose code it filled in (not overridden by the user).</summary>
    public IReadOnlySet<string> WikiIds => _wikiIds;

    /// <summary>Stratagems for which the wiki lists another code than the one in use; never applied automatically.</summary>
    public IReadOnlyList<WikiProposal> WikiProposals => _wikiProposals;

    /// <summary>The wiki rows this catalog was built with (empty without wiki data).</summary>
    public IReadOnlyList<WikiStratagem> Wiki => _wiki;

    /// <summary>Note of a built-in entry (e.g. "Verified in game …"), <c>null</c> if it has none.</summary>
    public string? Note(string id) => _notes.GetValueOrDefault(id);

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
        _wikiIds = other._wikiIds;
        _wikiProposals = other._wikiProposals;
        _wiki = other._wiki;
        _notes = other._notes;
        _categories = other._categories;
        UserFilePath = other.UserFilePath ?? UserFilePath;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The catalog shipped with the application (embedded <c>doc/stratagems.json</c>), optionally with the wiki layer.</summary>
    public static StratagemCatalog LoadSeed(IReadOnlyList<WikiStratagem>? wiki = null)
    {
        using var stream = OpenBuiltIn();
        return Parse(stream, null, wiki);
    }

    /// <summary>
    /// Built-in list, the wiki layer (<paramref name="wiki"/>, last stratagem table read from the wiki) and the
    /// user's own list <paramref name="userFile"/> (same format): an entry with the id of a built-in stratagem
    /// replaces it, a new id adds one, user categories add to the built-in ones. A missing user file is created
    /// as an empty template; an invalid one is ignored with a warning.
    /// </summary>
    public static StratagemCatalog Load(string userFile, ILogger logger, IReadOnlyList<WikiStratagem>? wiki = null)
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

            return WithoutUserList(userFile, wiki);
        }

        try
        {
            using var builtIn = OpenBuiltIn();
            using var user = File.OpenRead(userFile);
            var catalog = Parse(builtIn, user, wiki);
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
            return WithoutUserList(userFile, wiki);
        }
    }

    /// <summary>Loads the catalog again with its user list and new wiki data (or the current ones), e.g. after an edit.</summary>
    public void Reload(ILogger logger, IReadOnlyList<WikiStratagem>? wiki = null) =>
        ReplaceWith(UserFilePath is { } path ? Load(path, logger, wiki ?? _wiki) : WithoutUserList(null, wiki ?? _wiki));

    private static StratagemCatalog WithoutUserList(string? userFile, IReadOnlyList<WikiStratagem>? wiki)
    {
        using var builtIn = OpenBuiltIn();
        var catalog = Parse(builtIn, null, wiki);
        catalog.UserFilePath = userFile;
        return catalog;
    }

    /// <summary>
    /// Reads and validates a stratagem list in the format of <c>doc/stratagems.json</c>, optionally with the
    /// wiki layer and user overrides.
    /// </summary>
    /// <exception cref="FormatException">A list is not valid JSON or breaks a rule of the format.</exception>
    public static StratagemCatalog Parse(Stream json, Stream? overrides = null, IReadOnlyList<WikiStratagem>? wiki = null)
    {
        var list = Read(json);
        var categories = new Dictionary<string, string>(list.Categories ?? [], StringComparer.Ordinal);
        var items = Validate(list.Stratagems ?? [], categories, "built-in");
        var notes = items.Where(i => i.Notes is not null).ToDictionary(i => i.Id!, i => i.Notes!, StringComparer.Ordinal);
        var userIds = new HashSet<string>(StringComparer.Ordinal);
        var (wikiIds, wikiCodes) = ApplyWiki(items, categories, wiki ?? []);

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
        catalog._notes = notes;
        catalog._wiki = wiki ?? [];
        catalog._wikiIds = wikiIds.Where(id => !userIds.Contains(id)).ToHashSet(StringComparer.Ordinal);
        catalog._wikiProposals = wikiCodes
            .Where(w => catalog.Find(w.Key) is { } entry && !w.Value.Contains(entry.Sequence))
            .Select(w => new WikiProposal(w.Key, catalog.Find(w.Key)!.Sequence, w.Value[0]))
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToList();
        return catalog;
    }

    /// <summary>
    /// The wiki layer (wiki-source-plan §3.2): new stratagems are added, missing codes filled in, designations and
    /// short names follow the wiki page. A different code of a known stratagem is not applied – it becomes a proposal.
    /// </summary>
    /// <returns>Ids the wiki added or completed, and the valid wiki codes per matched id (for proposals).</returns>
    private static (HashSet<string> Ids, Dictionary<string, string[]> Codes) ApplyWiki(
        List<StratagemItem> items, Dictionary<string, string> categories, IReadOnlyList<WikiStratagem> wiki)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var codes = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (wiki.Count == 0)
        {
            return (ids, codes);
        }

        var keys = items.Select(i => new WikiMatchKey(i.Id!, i.Name!, i.Designation ?? i.Name!)).ToList();
        foreach (var group in WikiMatching.Match(wiki, keys).GroupBy(m => (m.Id, m.Row.Page)))
        {
            var row = group.First().Row;
            var valid = group.Select(m => m.Row.Code).Where(IsValidCode).Select(code => code!).Distinct().ToArray();
            var id = group.Key.Id;
            if (id is not null)
            {
                var index = items.FindIndex(i => i.Id == id);
                var item = items[index];
                if (item.Code is null && valid.Length > 0)
                {
                    ids.Add(id);
                }

                items[index] = item with { Designation = row.Page, Name = row.ShortName, Code = item.Code ?? valid.FirstOrDefault() };
                if (valid.Length > 0)
                {
                    codes[id] = valid;
                }

                continue;
            }

            id = row.NewId;
            if (id.Length == 0 || !IdPattern().IsMatch(id) || items.Any(i => i.Id == id) || !categories.ContainsKey(row.Category))
            {
                continue;   // no usable id, or a page the slug matching could not tell apart
            }

            items.Add(new StratagemItem(id, row.ShortName, row.Category, valid.FirstOrDefault(), row.Page));
            ids.Add(id);
            if (valid.Length > 0)
            {
                codes[id] = valid;
            }
        }

        return (ids, codes);
    }

    private static bool IsValidCode(string? code) => code is { Length: > 0 and <= MaxSequenceLength } && CodePattern().IsMatch(code);

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

    /// <summary>One line of the list.</summary>
    private sealed record StratagemItem(string? Id, string? Name, string? Category, string? Code, string? Designation = null, string? Notes = null);
}
