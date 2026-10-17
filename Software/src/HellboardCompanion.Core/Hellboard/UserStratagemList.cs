// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HellboardCompanion.Core.Hellboard;

/// <summary>A stratagem of the user's own list (added, or replacing a built-in one).</summary>
/// <param name="Code">Arrow sequence of U/D/L/R; <c>null</c> = not known yet.</param>
public sealed record UserStratagem(string Id, string Name, string Category, string? Code);

/// <summary>
/// The user's own stratagem list <c>%APPDATA%\HellboardCompanion\stratagems.json</c> (implementation plan
/// §6.11, §6.12), edited by the stratagem editor. Same format as <c>doc/stratagems.json</c>, written with
/// one stratagem per line.
/// </summary>
public sealed partial class UserStratagemList
{
    public const int MinCodeLength = 3;
    public const int MaxCodeLength = 10;

    public const string Comment =
        "Your own stratagems and corrections for Hellboard Companion. Same format as doc/stratagems.json of the Hellboard project " +
        "(described in doc/stratagems.md): an entry with the id of a built-in stratagem replaces it, a new id adds a stratagem. " +
        "Codes use U, D, L, R. Categories: Orbital, Eagle, Support weapon, Backpack, Vehicle, Defensive, Mission. " +
        "Edit it with the Stratagems tab of the Companion, or by hand and restart the Companion. " +
        "Example entry: {\"id\": \"my_stratagem\", \"name\": \"My Stratagem\", \"category\": \"Mission\", \"code\": \"UDLR\"}";

    private readonly List<UserStratagem> _entries;
    private readonly Dictionary<string, string> _categories;

    private UserStratagemList(string path, List<UserStratagem> entries, Dictionary<string, string> categories)
    {
        FilePath = path;
        _entries = entries;
        _categories = categories;
    }

    public string FilePath { get; }

    public IReadOnlyList<UserStratagem> Entries => _entries;

    /// <summary>Categories defined by the user (in addition to the built-in ones).</summary>
    public IReadOnlyDictionary<string, string> Categories => _categories;

    /// <summary>Reads the list; a missing file is an empty list.</summary>
    /// <exception cref="FormatException">The file is not a valid stratagem list.</exception>
    public static UserStratagemList Load(string path)
    {
        if (!File.Exists(path))
        {
            return new UserStratagemList(path, [], new(StringComparer.Ordinal));
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new FormatException("The stratagem list is not a JSON object.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"The stratagem list is not valid JSON: {e.Message}", e);
        }

        var categories = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root["categories"] is JsonObject userCategories)
        {
            foreach (var (name, color) in userCategories)
            {
                categories[name] = color?.GetValue<string>() ?? "white";
            }
        }

        var entries = new List<UserStratagem>();
        foreach (var item in (root["stratagems"] as JsonArray ?? []).OfType<JsonObject>())
        {
            entries.Add(new UserStratagem(
                item["id"]?.GetValue<string>() ?? string.Empty,
                item["name"]?.GetValue<string>() ?? string.Empty,
                item["category"]?.GetValue<string>() ?? string.Empty,
                item["code"]?.GetValue<string>()));
        }

        return new UserStratagemList(path, entries, categories);
    }

    public UserStratagem? Find(string id) => _entries.FirstOrDefault(e => e.Id == id);

    /// <summary>Adds or replaces the entry with the same id.</summary>
    public void Set(UserStratagem stratagem)
    {
        var index = _entries.FindIndex(e => e.Id == stratagem.Id);
        if (index >= 0)
        {
            _entries[index] = stratagem;
        }
        else
        {
            _entries.Add(stratagem);
        }
    }

    public bool Remove(string id) => _entries.RemoveAll(e => e.Id == id) > 0;

    /// <summary>Merges the entries of another list file (import); returns the number of imported entries.</summary>
    /// <exception cref="FormatException">The file is not a valid stratagem list.</exception>
    public int Import(string path)
    {
        var other = Load(path);
        foreach (var (name, color) in other.Categories)
        {
            _categories[name] = color;
        }

        foreach (var entry in other.Entries)
        {
            Set(entry);
        }

        return other.Entries.Count;
    }

    /// <summary>Writes the list (atomically, one stratagem per line).</summary>
    public void Save() => Write(FilePath);

    /// <summary>Writes a copy of the list, e.g. to share it (export).</summary>
    public void Export(string path) => Write(path);

    /// <summary>Error message for an entry, or <c>null</c> if it is valid.</summary>
    /// <param name="categories">All known categories (built-in and user).</param>
    public static string? Validate(UserStratagem stratagem, IReadOnlyCollection<string> categories)
    {
        if (!IdPattern().IsMatch(stratagem.Id))
        {
            return "The id may only contain a-z, 0-9 and _.";
        }

        if (string.IsNullOrWhiteSpace(stratagem.Name) || stratagem.Name.Length > 64)
        {
            return "The name must have 1 to 64 characters.";
        }

        if (!categories.Contains(stratagem.Category))
        {
            return $"Unknown category '{stratagem.Category}'.";
        }

        if (stratagem.Code is { } code && (code.Length is < MinCodeLength or > MaxCodeLength || code.Any(c => "UDLR".IndexOf(c) < 0)))
        {
            return $"The code must have {MinCodeLength} to {MaxCodeLength} arrows.";
        }

        return null;
    }

    private void Write(string path)
    {
        var categories = new JsonObject();
        foreach (var (name, color) in _categories)
        {
            categories[name] = color;
        }

        var text = new StringBuilder();
        text.Append("{\n");
        text.Append("  \"$comment\": ").Append(JsonSerializer.Serialize(Comment)).Append(",\n");
        text.Append("  \"version\": 1,\n");
        if (_categories.Count > 0)
        {
            text.Append("  \"categories\": ").Append(categories.ToJsonString()).Append(",\n");
        }

        text.Append("  \"stratagems\": [\n");
        text.Append(string.Join(",\n", _entries.Select(e => "    " + new JsonObject
        {
            ["id"] = e.Id, ["name"] = e.Name, ["category"] = e.Category, ["code"] = e.Code,
        }.ToJsonString())));
        text.Append(_entries.Count > 0 ? "\n  ]\n}\n" : "  ]\n}\n");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, text.ToString().ReplaceLineEndings());
        File.Move(temp, path, overwrite: true);
    }

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex IdPattern();
}
