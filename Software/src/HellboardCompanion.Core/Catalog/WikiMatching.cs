// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Catalog;

/// <summary>A catalog entry as far as matching it with the wiki needs it.</summary>
public sealed record WikiMatchKey(string Id, string Name, string Designation);

/// <summary>
/// Assigns the rows of the wiki's stratagem table to catalog ids (wiki-source-plan §3.1). Used by the
/// Companion (wiki layer of the catalog) and by the stratagem-sync tool, so both match the same way.
/// </summary>
public static class WikiMatching
{
    /// <summary>
    /// A row belongs to the entry whose designation is its page name. Rows without such an entry are matched
    /// by the slug of their page, title or icon name against the entry's id, name and designation, so a
    /// renamed wiki page shows up as a new designation instead of a second stratagem. Icon names shared by
    /// several rows (e.g. "Drill") are not used.
    /// </summary>
    /// <returns>Every row with the id of its catalog entry, <c>null</c> for a stratagem the catalog does not know.</returns>
    public static IReadOnlyList<(WikiStratagem Row, string? Id)> Match(IReadOnlyList<WikiStratagem> rows, IReadOnlyCollection<WikiMatchKey> entries)
    {
        var sharedIcons = rows.GroupBy(r => StratagemIcon.ToId(r.IconName)).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var byDesignation = entries.GroupBy(e => e.Designation, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
        var bySlug = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            foreach (var slug in new[] { entry.Id, StratagemIcon.ToId(entry.Name), StratagemIcon.ToId(entry.Designation) })
            {
                bySlug.TryAdd(slug, entry.Id);
            }
        }

        return rows.Select(row =>
        {
            if (byDesignation.TryGetValue(row.Page, out var id))
            {
                return (row, (string?)id);
            }

            var icon = StratagemIcon.ToId(row.IconName);
            string[] keys = [StratagemIcon.ToId(row.Page), StratagemIcon.ToId(row.Title), .. icon.Length == 0 || sharedIcons.Contains(icon) ? Array.Empty<string>() : [icon]];
            return (row, keys.Select(k => bySlug.GetValueOrDefault(k)).FirstOrDefault(i => i is not null));
        }).ToList();
    }
}
