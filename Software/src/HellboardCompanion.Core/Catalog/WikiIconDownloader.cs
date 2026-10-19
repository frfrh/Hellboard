// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Recognition;

namespace HellboardCompanion.Core.Catalog;

/// <summary>Outcome of an icon update (wiki-source-plan §4.1). File names are the wiki's names of the plain icons.</summary>
/// <param name="Checked"><c>false</c>: skipped, the wiki was checked less than a day ago.</param>
/// <param name="NotModified">Nothing changed since the last check (kept for callers; the wiki reports per file).</param>
public sealed record IconUpdateResult(
    bool Checked,
    bool NotModified,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Updated,
    IReadOnlyList<string> Moved,
    IReadOnlyList<string> Retired,
    DateTimeOffset? CheckedAt)
{
    public bool HasChanges => Added.Count + Updated.Count + Moved.Count + Retired.Count > 0;

    public string Summary => !Checked
        ? "Checked less than a day ago"
        : NotModified || !HasChanges
            ? "Icons are up to date"
            : string.Join(", ", new[]
            {
                Added.Count > 0 ? $"{Added.Count} new" : null,
                Updated.Count > 0 ? $"{Updated.Count} updated" : null,
                Moved.Count > 0 ? $"{Moved.Count} moved" : null,
                Retired.Count > 0 ? $"{Retired.Count} removed on the wiki (kept)" : null,
            }.OfType<string>());
}

/// <summary>The icon a catalog entry needs from the wiki.</summary>
/// <param name="Image">Icon file named by the wiki's stratagem table, e.g. "Cremator Stratagem Icon Background.svg".</param>
/// <param name="Recognizable"><c>false</c> if the image cannot tell this stratagem apart from others (shared icon).</param>
public sealed record WikiIconRequest(string Id, string Name, string Type, string Image, bool Recognizable)
{
    /// <summary>
    /// The icons the catalog needs, from the wiki's stratagem table (wiki-source-plan §4). Files shared by
    /// several stratagems (the drills, Upload Data …) are recognizable only for the stratagem the file is
    /// named after; placeholder icons of unreleased stratagems are left out.
    /// </summary>
    public static IReadOnlyList<WikiIconRequest> From(IReadOnlyList<WikiStratagem> wiki, StratagemCatalog catalog)
    {
        var keys = catalog.Entries.Select(e => new WikiMatchKey(e.Id, e.Name, e.FullName)).ToList();
        var requests = WikiMatching.Match(wiki, keys)
            .Select(m => (Id: m.Id ?? m.Row.NewId, m.Row))
            .Where(m => catalog.Find(m.Id) is not null && m.Row.IconName.Length > 0)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .ToList();
        var shared = requests.GroupBy(r => WikiIconDownloader.PlainName(r.Row.Image)).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        return requests
            .Select(r => new WikiIconRequest(r.Id, r.Row.ShortName, r.Row.Type, r.Row.Image,
                !shared.Contains(WikiIconDownloader.PlainName(r.Row.Image)) || StratagemIcon.ToId(r.Row.IconName) == StratagemIcon.ToId(r.Row.ShortName)))
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>
/// Keeps the local copy of the stratagem icons of helldivers.wiki.gg up to date (wiki-source-plan §4.1). The
/// icons are used read-only as templates and are never committed or bundled. At most one check per day
/// (unless forced): the MediaWiki API reports URL and SHA-1 of every file, only new and changed files are
/// downloaded. If the wiki has only the icon with background (e.g. the Meltagun), the plain icon is derived
/// from it as PNG. Files removed on the wiki are kept.
/// </summary>
public sealed class WikiIconDownloader(HttpClient http, TimeProvider? time = null)
{
    public const string IndexFile = "index.json";
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);
    private const int TitlesPerRequest = 50;
    private const int MaxParallelDownloads = 4;

    private static readonly JsonSerializerOptions IndexOptions = new() { WriteIndented = true };

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>"Cremator Stratagem Icon Background.svg" → "Cremator Stratagem Icon.svg" (spaces instead of underscores).</summary>
    public static string PlainName(string image) => Normalize(image).Replace(" Background.", ".", StringComparison.Ordinal);

    /// <summary>Updates the files of <paramref name="requests"/> in <paramref name="directory"/> and writes the icon list.</summary>
    /// <exception cref="HttpRequestException">The wiki is not reachable.</exception>
    /// <exception cref="InvalidDataException">An answer of the wiki does not have the expected structure.</exception>
    public async Task<IconUpdateResult> UpdateAsync(string directory, IReadOnlyList<WikiIconRequest> requests, bool force = false,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var index = LoadIndex(directory);
        var now = _time.GetUtcNow();
        var plain = requests.Select(r => PlainName(r.Image)).Distinct(StringComparer.Ordinal).ToList();
        // Also due for an icon the last check did not ask for (new stratagem on the wiki) or whose file was deleted.
        var due = force || index.CheckedAt is not { } last || now - last >= CheckInterval
            || plain.Any(name => !index.Checked.Contains(name) || (index.Files.ContainsKey(name) && LocalFile(directory, name) is null));

        var added = new List<string>();
        var updated = new List<string>();
        if (due)
        {
            var files = await GetFileInfoAsync(plain, cancellationToken);
            var withBackground = plain.Where(name => !files.ContainsKey(name)).ToDictionary(BackgroundName, name => name, StringComparer.Ordinal);
            foreach (var (name, info) in await GetFileInfoAsync([.. withBackground.Keys], cancellationToken))
            {
                files[withBackground[name]] = info with { FromBackground = true };
            }

            var downloads = files
                .Where(f => !index.Files.TryGetValue(f.Key, out var sha1) || sha1 != f.Value.Sha1 || LocalFile(directory, f.Key) is null)
                .ToList();
            await Parallel.ForEachAsync(
                downloads,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = cancellationToken },
                async (file, token) => await DownloadAsync(directory, file.Key, file.Value, token));
            foreach (var (name, info) in downloads)
            {
                (index.Files.ContainsKey(name) ? updated : added).Add(name);
                index.Files[name] = info.Sha1;
            }

            index.CheckedAt = now;
            index.Checked = [.. plain];
        }

        index.Icons = requests
            .Select(r => (Request: r, File: LocalFile(directory, PlainName(r.Image))))
            .Where(r => r.File is not null)
            .Select(r => new IndexIcon(r.Request.Id, r.Request.Name, r.Request.Type, Path.GetFileName(r.File!), r.Request.Recognizable))
            .ToList();
        SaveIndex(directory, index);
        return new IconUpdateResult(due, false, added, updated, [], [], index.CheckedAt);
    }

    /// <summary>The icons of a local copy (icon list of the last update), only those whose file exists.</summary>
    public static IReadOnlyList<StratagemIcon> LoadIcons(string directory) =>
        LoadIndex(directory).Icons
            .Select(i => new StratagemIcon(i.Id, i.Name, i.Type, Path.Combine(directory, i.File), i.Recognizable))
            .Where(i => File.Exists(i.ImagePath))
            .ToList();

    /// <summary>Writes the icon list of <paramref name="directory"/> (the files must be in it), e.g. for a copy of icons.</summary>
    public static void WriteIconList(string directory, IEnumerable<StratagemIcon> icons)
    {
        var index = LoadIndex(directory);
        index.Icons = icons.Select(i => new IndexIcon(i.Id, i.Name, i.Group, Path.GetFileName(i.ImagePath), i.Recognizable)).ToList();
        SaveIndex(directory, index);
    }

    private async Task<Dictionary<string, FileInfo>> GetFileInfoAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, FileInfo>(StringComparer.Ordinal);
        foreach (var chunk in names.Chunk(TitlesPerRequest))
        {
            var titles = Uri.EscapeDataString(string.Join("|", chunk.Select(n => $"File:{n}")));
            var url = $"{WikiClient.Api}?action=query&format=json&prop=imageinfo&iiprop=url%7Csha1&titles={titles}";
            try
            {
                using var document = JsonDocument.Parse(await http.GetStringAsync(url, cancellationToken));
                foreach (var page in document.RootElement.GetProperty("query").GetProperty("pages").EnumerateObject().Select(p => p.Value))
                {
                    if (page.TryGetProperty("imageinfo", out var info) && info.GetArrayLength() > 0)
                    {
                        var name = Normalize(page.GetProperty("title").GetString()!["File:".Length..]);
                        result[name] = new FileInfo(info[0].GetProperty("url").GetString()!, info[0].GetProperty("sha1").GetString()!, false);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                throw new InvalidDataException($"Unexpected file information from the wiki: {ex.Message}", ex);
            }
        }

        return result;
    }

    private async Task DownloadAsync(string directory, string name, FileInfo info, CancellationToken cancellationToken)
    {
        var content = await http.GetByteArrayAsync(info.Url, cancellationToken);
        var svg = Path.Combine(directory, FileName(name));
        if (!info.FromBackground)
        {
            await WriteAsync(svg, content, cancellationToken);
            Delete(Path.ChangeExtension(svg, ".png"));   // a plain icon replaces a derived one
            return;
        }

        // Only the icon with background exists: keep it next to the derived plain icon (the PNG is used).
        var background = Path.ChangeExtension(svg, ".background.svg");
        await WriteAsync(background, content, cancellationToken);
        await WriteAsync(Path.ChangeExtension(svg, ".png"), SvgRenderer.DeriveFromBackground(background), cancellationToken);
        Delete(svg);
    }

    /// <summary>The local file of an icon: the plain SVG, else the PNG derived from the icon with background.</summary>
    private static string? LocalFile(string directory, string name)
    {
        var svg = Path.Combine(directory, FileName(name));
        var png = Path.ChangeExtension(svg, ".png");
        return File.Exists(svg) ? svg : File.Exists(png) ? png : null;
    }

    private static string BackgroundName(string plainName) => Path.GetFileNameWithoutExtension(plainName) + " Background" + Path.GetExtension(plainName);

    /// <summary>Wiki file names use spaces and underscores alike and start with a capital letter.</summary>
    private static string Normalize(string name)
    {
        var spaced = name.Replace('_', ' ').Trim();
        return spaced.Length == 0 ? spaced : char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    private static string FileName(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static async Task WriteAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        await File.WriteAllBytesAsync(path + ".tmp", content, cancellationToken);
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static IconIndex LoadIndex(string directory)
    {
        try
        {
            if (JsonSerializer.Deserialize<IconIndex>(File.ReadAllText(Path.Combine(directory, IndexFile))) is { } index)
            {
                index.Files ??= new Dictionary<string, string>(StringComparer.Ordinal);
                index.Checked ??= [];
                index.Icons ??= [];
                return index;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // missing or broken: start over (files are checked against the wiki again)
        }

        return new IconIndex();
    }

    private static void SaveIndex(string directory, IconIndex index)
    {
        var path = Path.Combine(directory, IndexFile);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(index, IndexOptions));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private sealed record FileInfo(string Url, string Sha1, bool FromBackground);

    private sealed class IconIndex
    {
        [JsonPropertyName("checkedAt")]
        public DateTimeOffset? CheckedAt { get; set; }

        /// <summary>Wiki file name of the plain icon → SHA-1 of the downloaded file (plain, or with background).</summary>
        [JsonPropertyName("files")]
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Plain icon names asked for at the last check (also those the wiki does not have).</summary>
        [JsonPropertyName("checked")]
        public HashSet<string> Checked { get; set; } = new(StringComparer.Ordinal);

        [JsonPropertyName("icons")]
        public List<IndexIcon> Icons { get; set; } = [];
    }

    private sealed record IndexIcon(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("file")] string File,
        [property: JsonPropertyName("recognizable")] bool Recognizable);
}
