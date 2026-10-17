// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.Core.Catalog;

/// <summary>
/// Local copy of the stratagem icons in the user data directory (implementation plan §6.11).
/// Never committed or bundled; downloaded at runtime and checked for changes at most once a day.
/// </summary>
public sealed class StratagemIconStore(IPlatformPaths paths, ILogger<StratagemIconStore> logger)
{
    private readonly SemaphoreSlim _updating = new(1, 1);
    private volatile IReadOnlyList<StratagemIcon> _icons = [];

    public string Directory { get; } = Path.Combine(paths.DataDirectory, "icons");

    /// <summary>Currently available icons; empty until <see cref="UpdateAsync"/> succeeded once.</summary>
    public IReadOnlyList<StratagemIcon> Icons => _icons;

    /// <summary>Result of the last update, <c>null</c> before the first one.</summary>
    public IconUpdateResult? LastResult { get; private set; }

    /// <summary>Error of the last update attempt (offline, API limit …), <c>null</c> if it succeeded.</summary>
    public string? LastError { get; private set; }

    /// <summary>Raised after the icons were (re)loaded, e.g. because some were added or changed.</summary>
    public event EventHandler? IconsChanged;

    /// <summary>Raised after every update attempt (also when nothing changed or it failed).</summary>
    public event EventHandler? UpdateFinished;

    /// <summary>
    /// Updates the local copy (keeps it when offline) and reloads the icons if something changed.
    /// Without <paramref name="force"/> the repository is checked at most once a day.
    /// </summary>
    public async Task UpdateAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _updating.WaitAsync(cancellationToken);
        try
        {
            var firstLoad = _icons.Count == 0;
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"HellboardCompanion/{AppInfo.Version}");
                var result = await new StratagemIconDownloader(http).UpdateAsync(Directory, force, cancellationToken);
                LastResult = result;
                LastError = null;
                if (result.Checked)
                {
                    logger.LogInformation("Stratagem icons: {Summary}", result.Summary);
                    foreach (var path in result.Added.Concat(result.Updated).Concat(result.Moved).Concat(result.Retired))
                    {
                        logger.LogDebug("Stratagem icon changed: {Path}", path);
                    }
                }

                if (firstLoad || result.HasChanges)
                {
                    LoadLocal();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or IOException)
            {
                LastError = ex.Message;
                logger.LogWarning(ex, "Stratagem icons could not be updated, using the local copy");
                if (firstLoad)
                {
                    LoadLocal();
                }
            }
        }
        finally
        {
            _updating.Release();
            UpdateFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Loads the icons that are already in <see cref="Directory"/>, without network access.</summary>
    public void LoadLocal()
    {
        var icons = System.IO.Directory.Exists(Directory) ? StratagemIcon.LoadDirectory(Directory) : [];
        logger.LogInformation("Stratagem icons available: {Count}", icons.Count);
        _icons = icons;
        IconsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Icons of stratagems the catalog has no input code for – new in the repository (§6.11 step 6, editor §6.12).</summary>
    public IReadOnlyList<StratagemIcon> WithoutCode(StratagemCatalog catalog) =>
        _icons.Where(icon => catalog.Find(icon.Id) is not { IsTransferable: true }).ToList();
}
