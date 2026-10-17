// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HellboardCompanion.Core.Catalog;

/// <summary>Outcome of an icon update (implementation plan §6.11). Paths are relative to the icon directory, with '/'.</summary>
/// <param name="Checked"><c>false</c>: skipped, the repository was checked less than a day ago.</param>
/// <param name="NotModified">The repository did not change since the last check (HTTP 304).</param>
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
                Retired.Count > 0 ? $"{Retired.Count} removed from the repository (kept)" : null,
            }.OfType<string>());
}

/// <summary>
/// Keeps the local copy of the stratagem SVGs of <c>nvigneux/Helldivers-2-Stratagems-icons-svg</c> up to
/// date (implementation plan §6.11). The icons are used read-only as templates and are never committed
/// or bundled. At most one check per day (unless forced), conditional request with the ETag, changes
/// detected by the git blob SHA: new and changed files are downloaded, moved files renamed locally,
/// removed files kept (old history and screenshots still use them).
/// </summary>
public sealed class StratagemIconDownloader(HttpClient http, TimeProvider? time = null)
{
    public const string Repository = "nvigneux/Helldivers-2-Stratagems-icons-svg";
    public const string Branch = "master";
    public const string IndexFile = "index.json";
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);
    private const int MaxParallelDownloads = 6;

    private static readonly JsonSerializerOptions IndexOptions = new() { WriteIndented = true };

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<IconUpdateResult> UpdateAsync(string directory, bool force = false, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var index = LoadIndex(directory);
        var now = _time.GetUtcNow();
        var local = Directory.EnumerateFiles(directory, "*.svg", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(directory, path).Replace('\\', '/'), StringComparer.Ordinal);

        if (!force && local.Count > 0 && index.CheckedAt is { } last && now - last < CheckInterval)
        {
            return new IconUpdateResult(false, false, [], [], [], [], last);
        }

        // Icons downloaded by older versions have no index entry: compute their SHA instead of downloading again.
        foreach (var (path, fullPath) in local)
        {
            if (!index.Files.ContainsKey(path))
            {
                index.Files[path] = GitBlobSha(await File.ReadAllBytesAsync(fullPath, cancellationToken));
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/git/trees/{Branch}?recursive=1");
        if (index.ETag is not null && local.Count > 0)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", index.ETag);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            index.CheckedAt = now;
            SaveIndex(directory, index);
            return new IconUpdateResult(true, true, [], [], [], [], now);
        }

        response.EnsureSuccessStatusCode();
        var tree = await response.Content.ReadFromJsonAsync<GitTree>(cancellationToken)
            ?? throw new InvalidDataException("Empty response from the GitHub API.");
        var remote = tree.Entries
            .Where(e => e.Type == "blob" && e.Path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(e => e.Path, e => e.Sha, StringComparer.Ordinal);

        var downloads = new List<(string Path, string Target)>();
        var added = new List<string>();
        var updated = new List<string>();
        var moved = new List<string>();
        var byLocalSha = index.Files
            .Where(f => !remote.ContainsKey(f.Key) && local.ContainsKey(f.Key))
            .GroupBy(f => f.Value)
            .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.Ordinal);

        foreach (var (path, sha) in remote)
        {
            var target = Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar));
            if (index.Files.TryGetValue(path, out var known) && local.ContainsKey(path))
            {
                if (known != sha)
                {
                    downloads.Add((path, target));
                    updated.Add(path);
                }
            }
            else if (byLocalSha.Remove(sha, out var oldPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(local[oldPath], target, overwrite: true);
                index.Files.Remove(oldPath);
                moved.Add($"{oldPath} → {path}");
            }
            else
            {
                downloads.Add((path, target));
                added.Add(path);
            }
        }

        // Several downloads at once: the first start loads all ~110 icons.
        await Parallel.ForEachAsync(
            downloads,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = cancellationToken },
            async (download, token) => await DownloadAsync(download.Path, download.Target, token));
        foreach (var (path, sha) in remote)
        {
            index.Files[path] = sha;   // only after all downloads succeeded, so a failed one is retried next time
        }

        var retired = index.Files.Keys
            .Where(path => !remote.ContainsKey(path) && !index.Retired.Contains(path))
            .Order(StringComparer.Ordinal)
            .ToList();
        index.Retired.UnionWith(retired);
        index.Retired.RemoveWhere(remote.ContainsKey);
        index.ETag = response.Headers.ETag?.ToString();
        index.CheckedAt = now;
        SaveIndex(directory, index);
        return new IconUpdateResult(true, false, added, updated, moved, retired, now);
    }

    /// <summary>The SHA-1 git uses for a file ("blob &lt;size&gt;\0" + content), as reported by the trees API.</summary>
    public static string GitBlobSha(byte[] content)
    {
        var header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
        return Convert.ToHexStringLower(SHA1.HashData([.. header, .. content]));
    }

    private async Task DownloadAsync(string path, string target, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var url = $"https://raw.githubusercontent.com/{Repository}/{Branch}/{Uri.EscapeDataString(path).Replace("%2F", "/")}";
        var content = await http.GetByteArrayAsync(url, cancellationToken);
        var temp = target + ".tmp";
        await File.WriteAllBytesAsync(temp, content, cancellationToken);
        File.Move(temp, target, overwrite: true);
    }

    private static IconIndex LoadIndex(string directory)
    {
        try
        {
            var index = JsonSerializer.Deserialize<IconIndex>(File.ReadAllText(Path.Combine(directory, IndexFile)));
            if (index is not null)
            {
                return index;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // missing or broken: start over (local files are hashed again)
        }

        return new IconIndex();
    }

    private static void SaveIndex(string directory, IconIndex index)
    {
        var path = Path.Combine(directory, IndexFile);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(index, IndexOptions));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private sealed class IconIndex
    {
        [JsonPropertyName("etag")]
        public string? ETag { get; set; }

        [JsonPropertyName("checkedAt")]
        public DateTimeOffset? CheckedAt { get; set; }

        /// <summary>Relative path → git blob SHA.</summary>
        [JsonPropertyName("files")]
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Paths that were removed from the repository; their files are kept.</summary>
        [JsonPropertyName("retired")]
        public HashSet<string> Retired { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed record GitTree([property: JsonPropertyName("tree")] IReadOnlyList<GitTreeEntry> Entries);

    private sealed record GitTreeEntry(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("sha")] string Sha);
}
