// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace HellboardCompanion.Core.Catalog;

/// <summary>
/// Downloads the stratagem SVGs of <c>nvigneux/Helldivers-2-Stratagems-icons-svg</c> into a local
/// directory (implementation plan §6.11). The icons are used read-only as templates and are never
/// committed or bundled. Minimal version: downloads missing files; change detection via blob SHA,
/// ETag and the daily schedule follow with the catalog updater (P6).
/// </summary>
public sealed class StratagemIconDownloader(HttpClient http)
{
    public const string Repository = "nvigneux/Helldivers-2-Stratagems-icons-svg";
    public const string Branch = "master";

    /// <returns>Number of newly downloaded files.</returns>
    public async Task<int> DownloadMissingAsync(string targetDirectory, CancellationToken cancellationToken = default)
    {
        var tree = await http.GetFromJsonAsync<GitTree>(
            $"https://api.github.com/repos/{Repository}/git/trees/{Branch}?recursive=1", cancellationToken)
            ?? throw new InvalidDataException("Empty response from the GitHub API.");

        var downloaded = 0;
        foreach (var entry in tree.Entries.Where(e => e.Type == "blob" && e.Path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)))
        {
            var target = Path.Combine(targetDirectory, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(target))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var url = $"https://raw.githubusercontent.com/{Repository}/{Branch}/{Uri.EscapeDataString(entry.Path).Replace("%2F", "/")}";
            var content = await http.GetByteArrayAsync(url, cancellationToken);
            await File.WriteAllBytesAsync(target, content, cancellationToken);
            downloaded++;
        }

        return downloaded;
    }

    private sealed record GitTree([property: JsonPropertyName("tree")] IReadOnlyList<GitTreeEntry> Entries);

    private sealed record GitTreeEntry(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type);
}
