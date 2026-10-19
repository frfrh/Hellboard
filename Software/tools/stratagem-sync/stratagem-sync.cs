// SPDX-License-Identifier: GPL-3.0-or-later
//
// Compares doc/stratagems.json with the stratagem table of the Helldivers wiki (helldivers.wiki.gg, the
// authoritative source since 2026-10-19) and prints the differences: codes, designations, names, categories,
// stratagems missing on either side, with a suggested catalog line for every new stratagem. Read-only: the
// catalog is never changed by this tool; differences are checked and entered by hand (doc/stratagems.md).
// Reads the wiki with the same Core client as the Companion (WikiClient).
//
// Usage (from the repository root):  dotnet run Software/tools/stratagem-sync/stratagem-sync.cs -- [--catalog <path>]
// Exit code: 0 = no differences, 1 = differences found, 2 = error.

#:project ../../src/HellboardCompanion.Core/HellboardCompanion.Core.csproj
#:property PublishAot=false

using System.Text.Json;
using HellboardCompanion.Core;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;

var catalogPath = Option("--catalog") ?? Path.GetFullPath(Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", "../../../doc/stratagems.json"));

// --- Catalog -----------------------------------------------------------------------------------------
var catalog = new List<Entry>();
using (var document = JsonDocument.Parse(File.ReadAllText(catalogPath)))
{
    foreach (var s in document.RootElement.GetProperty("stratagems").EnumerateArray())
    {
        catalog.Add(new Entry(
            s.GetProperty("id").GetString()!,
            s.GetProperty("name").GetString()!,
            s.GetProperty("designation").GetString()!,
            s.GetProperty("category").GetString()!,
            s.GetProperty("code").GetString(),
            s.GetProperty("notes").GetString()));
    }
}

// --- Wiki ----------------------------------------------------------------------------------------------
IReadOnlyList<WikiStratagem> wiki;
try
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd($"HellboardCompanion-StratagemSync/{AppInfo.Version.Split('+')[0]} (+https://github.com/frfrh/Hellboard)");
    wiki = await new WikiClient(http).GetStratagemsAsync();
}
catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or TaskCanceledException)
{
    Console.Error.WriteLine($"Cannot read the stratagem table of helldivers.wiki.gg: {ex.Message}");
    return 2;
}

Console.WriteLine($"Catalog:  {catalogPath} ({catalog.Count} stratagems)");
Console.WriteLine($"wiki.gg:  {wiki.Count} stratagem rows ({WikiClient.StratagemsPage})");

// --- Matching ------------------------------------------------------------------------------------------
// A wiki row belongs to the catalog entry whose designation is its page name (rule of doc/stratagems.md).
// Rows without such an entry are matched by the slug of their page, title or icon name, so a renamed page
// shows up as a different designation instead of a new stratagem. Icon names shared by several rows are not used.
var sharedIcons = wiki.GroupBy(r => StratagemIcon.ToId(r.IconName)).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
var matches = catalog.ToDictionary(e => e, _ => new List<WikiStratagem>());
var unmatched = new List<WikiStratagem>();
foreach (var row in wiki)
{
    var icon = StratagemIcon.ToId(row.IconName);
    string[] keys = [StratagemIcon.ToId(row.Page), StratagemIcon.ToId(row.Title), .. icon.Length == 0 || sharedIcons.Contains(icon) ? Array.Empty<string>() : [icon]];
    var entry = catalog.FirstOrDefault(e => e.Designation == row.Page)
        ?? catalog.FirstOrDefault(e => new[] { e.Id, StratagemIcon.ToId(e.Name), StratagemIcon.ToId(e.Designation) }.Intersect(keys).Any());
    if (entry is null)
    {
        unmatched.Add(row);
    }
    else
    {
        matches[entry].Add(row);
    }
}

// --- Report --------------------------------------------------------------------------------------------
var differences = 0;

Section("Different codes", catalog
    .Where(e => matches[e].Count > 0 && matches[e].All(r => r.Code != e.Code))
    .Select(e => $"{e.Id,-32} catalog {Arrows(e.Code),-12} wiki.gg {string.Join(" / ", matches[e].Select(r => Arrows(r.Code)).Distinct())}" +
        (e.Notes is null ? string.Empty : $"\n{string.Empty,-34}note: {e.Notes}")));

Section("Several codes on wiki.gg (catalog uses one of them)", catalog
    .Where(e => matches[e].Select(r => r.Code).Distinct().Count() > 1 && matches[e].Any(r => r.Code == e.Code))
    .Select(e => $"{e.Id,-32} catalog {Arrows(e.Code),-12} wiki.gg {string.Join(" / ", matches[e].Select(r => Arrows(r.Code)).Distinct())}"));

Section("Different designations (designation must be the wiki page name)", catalog
    .Where(e => matches[e].Count > 0 && matches[e].All(r => r.Page != e.Designation))
    .Select(e => $"{e.Id,-32} catalog \"{e.Designation}\"  wiki.gg \"{matches[e][0].Page}\""));

Section("Names not following the rule (name = designation without model prefix)", catalog
    .Where(e => e.Name != StratagemCatalog.ShortName(e.Designation))
    .Select(e => $"{e.Id,-32} name \"{e.Name}\"  expected \"{StratagemCatalog.ShortName(e.Designation)}\""));

Section("Different categories", catalog
    .Where(e => matches[e].Count > 0 && matches[e].All(r => r.Category != e.Category))
    .Select(e => $"{e.Id,-32} catalog {e.Category,-15} wiki.gg {matches[e][0].Type} → {matches[e][0].Category}"));

Section("On wiki.gg, not in the catalog (suggested catalog lines)", unmatched
    .GroupBy(r => r.Page)
    .OrderBy(g => g.Key, StringComparer.Ordinal)
    .Select(g => $"{g.Key} ({g.First().Type}, {string.Join(" / ", g.Select(r => Arrows(r.Code)).Distinct())})\n  " + JsonLine(g.First())));

Section("In the catalog, not on wiki.gg", catalog
    .Where(e => matches[e].Count == 0)
    .Select(e => $"{e.Id,-32} {e.Designation}"));

Console.WriteLine();
Console.WriteLine(differences == 0 ? "No differences." : $"{differences} difference(s). Check them and enter corrections by hand (doc/stratagems.md).");
return differences == 0 ? 0 : 1;

// --- Helpers -------------------------------------------------------------------------------------------
void Section(string title, IEnumerable<string> lines)
{
    var list = lines.ToList();
    if (list.Count == 0)
    {
        return;
    }

    differences += list.Count;
    Console.WriteLine();
    Console.WriteLine($"{title}: {list.Count}");
    foreach (var line in list)
    {
        Console.WriteLine($"  {line}");
    }
}

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string Arrows(string? code) => code is null ? "(none)" : string.Concat(code.Select(c => c switch { 'U' => '↑', 'D' => '↓', 'L' => '←', 'R' => '→', _ => '?' }));

// Id from the icon name (closest to the names players know), the short name for a placeholder icon.
static string JsonLine(WikiStratagem row)
{
    var name = StratagemCatalog.ShortName(row.Page);
    var id = StratagemIcon.ToId(row.IconName.Length > 0 ? row.IconName : name);
    var code = row.Code is null ? "null" : $"\"{row.Code}\"";
    var notes = row.Code is null ? "No code on wiki.gg yet" : $"Code from wiki.gg ({DateTime.Now:yyyy-MM-dd}), not verified in game";
    return JsonSerializer.Serialize(new Dictionary<string, string?>
    {
        ["id"] = id, ["name"] = name, ["designation"] = row.Page, ["category"] = row.Category, ["code"] = row.Code, ["notes"] = notes,
    }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
        .Replace("\":", "\": ").Replace(",\"", ", \"") + ",";
}

internal sealed record Entry(string Id, string Name, string Designation, string Category, string? Code, string? Notes);
