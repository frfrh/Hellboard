// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;

namespace HellboardCompanion.Core.Tests.TestData;

/// <summary>
/// Local copy of the stratagem icons of helldivers.wiki.gg in <c>tests/testData/local/icons-wiki/</c> (not
/// committed, see implementation plan §3.3). Downloaded on first use and checked at most once a day; tests
/// are skipped when offline and no copy exists.
/// </summary>
public static class LocalIcons
{
    private static readonly Lazy<IReadOnlyList<StratagemIcon>?> Icons = new(Load);

    public static string Directory { get; } = Path.Combine(LocalScenes.TestDataDirectory, "local", "icons-wiki");

    public static IReadOnlyList<StratagemIcon> All
    {
        get
        {
            Assert.SkipWhen(Icons.Value is null, "Stratagem icons are not available (download failed).");
            return Icons.Value!;
        }
    }

    private static IReadOnlyList<StratagemIcon>? Load()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("HellboardCompanion-Tests (+https://github.com/frfrh/Hellboard)");
            var wiki = new WikiClient(http).GetStratagemsAsync().GetAwaiter().GetResult();
            var requests = WikiIconRequest.From(wiki, StratagemCatalog.LoadSeed(wiki));
            new WikiIconDownloader(http).UpdateAsync(Directory, requests).GetAwaiter().GetResult();   // at most once a day
        }
        catch (Exception) when (System.IO.Directory.Exists(Directory))
        {
            // Offline: use what is already there.
        }
        catch (Exception)
        {
            return null;
        }

        var icons = WikiIconDownloader.LoadIcons(Directory);
        return icons.Count > 0 ? icons : null;
    }
}
