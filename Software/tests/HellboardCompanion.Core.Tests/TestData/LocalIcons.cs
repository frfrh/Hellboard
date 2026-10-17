// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Catalog;

namespace HellboardCompanion.Core.Tests.TestData;

/// <summary>
/// Local copy of the stratagem icon repository in <c>tests/testData/local/icons/</c> (not committed,
/// see implementation plan §3.3). Downloaded on first use; tests are skipped when offline.
/// </summary>
public static class LocalIcons
{
    private static readonly Lazy<IReadOnlyList<StratagemIcon>?> Icons = new(Load);

    public static string Directory { get; } = Path.Combine(LocalScenes.TestDataDirectory, "local", "icons");

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
            http.DefaultRequestHeaders.UserAgent.ParseAdd("HellboardCompanion-Tests");
            new StratagemIconDownloader(http).UpdateAsync(Directory).GetAwaiter().GetResult();   // at most once a day
        }
        catch (Exception) when (System.IO.Directory.Exists(Directory))
        {
            // Offline: use what is already there.
        }
        catch (Exception)
        {
            return null;
        }

        var icons = StratagemIcon.LoadDirectory(Directory);
        return icons.Count > 0 ? icons : null;
    }
}
