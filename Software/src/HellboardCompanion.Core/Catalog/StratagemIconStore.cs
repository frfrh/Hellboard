// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.Core.Catalog;

/// <summary>
/// Local copy of the stratagem icons in the user data directory (implementation plan §6.11).
/// Never committed or bundled; downloaded at runtime.
/// </summary>
public sealed class StratagemIconStore(IPlatformPaths paths, ILogger<StratagemIconStore> logger)
{
    private volatile IReadOnlyList<StratagemIcon> _icons = [];

    public string Directory { get; } = Path.Combine(paths.DataDirectory, "icons");

    /// <summary>Currently available icons; empty until <see cref="UpdateAsync"/> succeeded once.</summary>
    public IReadOnlyList<StratagemIcon> Icons => _icons;

    public event EventHandler? IconsChanged;

    /// <summary>Downloads missing icons (keeps the existing ones when offline) and reloads the list.</summary>
    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"HellboardCompanion/{AppInfo.Version}");
            var downloaded = await new StratagemIconDownloader(http).DownloadMissingAsync(Directory, cancellationToken);
            logger.LogInformation("Stratagem icons: {Count} new icons downloaded", downloaded);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            logger.LogWarning(ex, "Stratagem icons could not be updated, using the local copy");
        }

        var icons = System.IO.Directory.Exists(Directory) ? StratagemIcon.LoadDirectory(Directory) : [];
        logger.LogInformation("Stratagem icons available: {Count}", icons.Count);
        _icons = icons;
        IconsChanged?.Invoke(this, EventArgs.Empty);
    }
}
