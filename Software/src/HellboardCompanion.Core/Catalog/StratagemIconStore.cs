// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.Core.Catalog;

/// <summary>
/// Local copy of the stratagem icons of helldivers.wiki.gg in the user data directory (wiki-source-plan §4).
/// Never committed or bundled; downloaded at runtime and checked for changes at most once a day, together
/// with the wiki's stratagem table (<see cref="WikiStratagemStore"/>), which says which icon belongs to which stratagem.
/// </summary>
public sealed class StratagemIconStore(IPlatformPaths paths, ILogger<StratagemIconStore> logger, WikiStratagemStore wiki, StratagemCatalog catalog,
    HttpMessageHandler? handler = null)
{
    private readonly SemaphoreSlim _updating = new(1, 1);
    private volatile IReadOnlyList<StratagemIcon> _icons = [];

    public string Directory { get; } = Path.Combine(paths.DataDirectory, "icons-wiki");

    /// <summary>Icons of the former icon repository (nvigneux); removed after the first download from the wiki.</summary>
    private string OldDirectory { get; } = Path.Combine(paths.DataDirectory, "icons");

    /// <summary>Currently available icons; empty until <see cref="UpdateAsync"/> succeeded once.</summary>
    public IReadOnlyList<StratagemIcon> Icons => _icons;

    /// <summary>Result of the last update, <c>null</c> before the first one.</summary>
    public IconUpdateResult? LastResult { get; private set; }

    /// <summary>Error of the last update attempt (offline, changed wiki …), <c>null</c> if it succeeded.</summary>
    public string? LastError { get; private set; }

    /// <summary>Raised after the icons were (re)loaded, e.g. because some were added or changed.</summary>
    public event EventHandler? IconsChanged;

    /// <summary>Raised after every update attempt (also when nothing changed or it failed).</summary>
    public event EventHandler? UpdateFinished;

    /// <summary>
    /// Updates the wiki's stratagem table and then the local icon copy (keeps both when offline) and reloads the
    /// icons if something changed. Without <paramref name="force"/> the wiki is checked at most once a day.
    /// </summary>
    public async Task UpdateAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _updating.WaitAsync(cancellationToken);
        try
        {
            await wiki.UpdateAsync(force, cancellationToken);   // a changed table rebuilds the catalog first
            var requests = WikiIconRequest.From(wiki.Stratagems, catalog);
            var before = _icons;
            try
            {
                if (requests.Count == 0)
                {
                    throw new InvalidDataException(wiki.LastError ?? "The stratagem table of the wiki is not available yet.");
                }

                using var http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
                http.Timeout = TimeSpan.FromSeconds(30);
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"HellboardCompanion/{AppInfo.Version.Split('+')[0]} (+https://github.com/frfrh/Hellboard)");
                var result = await new WikiIconDownloader(http).UpdateAsync(Directory, requests, force, cancellationToken);
                LastResult = result;
                LastError = null;
                if (result.Checked)
                {
                    logger.LogInformation("Stratagem icons: {Summary}", result.Summary);
                    foreach (var path in result.Added.Concat(result.Updated))
                    {
                        logger.LogDebug("Stratagem icon changed: {Path}", path);
                    }
                }

                RemoveOldIcons();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                LastError = ex.Message;
                logger.LogWarning(ex, "Stratagem icons could not be updated, using the local copy");
            }

            var icons = WikiIconDownloader.LoadIcons(Directory);
            if (before.Count == 0 || LastResult?.HasChanges == true || !icons.SequenceEqual(before))
            {
                Use(icons);
            }
        }
        finally
        {
            _updating.Release();
            UpdateFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Loads the icons that are already in <see cref="Directory"/>, without network access.</summary>
    public void LoadLocal() => Use(WikiIconDownloader.LoadIcons(Directory));

    /// <summary>Stratagems of the catalog without input code that have an icon (new on the wiki, editor §6.12).</summary>
    public IReadOnlyList<StratagemIcon> WithoutCode(StratagemCatalog stratagems) =>
        _icons.Where(icon => stratagems.Find(icon.Id) is not { IsTransferable: true }).ToList();

    private void Use(IReadOnlyList<StratagemIcon> icons)
    {
        logger.LogInformation("Stratagem icons available: {Count} ({Recognizable} for recognition)", icons.Count, icons.Count(i => i.Recognizable));
        _icons = icons;
        IconsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The icon cache of the former icon repository is not needed once the wiki icons are there.</summary>
    private void RemoveOldIcons()
    {
        if (!System.IO.Directory.Exists(OldDirectory) || WikiIconDownloader.LoadIcons(Directory).Count == 0)
        {
            return;
        }

        try
        {
            System.IO.Directory.Delete(OldDirectory, recursive: true);
            logger.LogInformation("Icons of the former icon repository removed: {Directory}", OldDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Icons of the former icon repository could not be removed: {Error}", ex.Message);
        }
    }
}
