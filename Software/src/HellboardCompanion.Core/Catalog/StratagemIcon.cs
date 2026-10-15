// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;

namespace HellboardCompanion.Core.Catalog;

/// <summary>
/// A stratagem icon from the icon repository (implementation plan §6.11): file name = display name,
/// folder = ship module or warbond.
/// </summary>
public sealed record StratagemIcon(string Id, string Name, string Group, string SvgPath)
{
    /// <summary>Stable id derived from the display name, e.g. "Eagle 500KG Bomb" → "eagle_500kg_bomb".</summary>
    public static string ToId(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString().TrimEnd('_');
    }

    /// <summary>Reads all icons of a local copy of the repository (<c>&lt;root&gt;/&lt;group&gt;/&lt;name&gt;.svg</c>).</summary>
    public static IReadOnlyList<StratagemIcon> LoadDirectory(string root) =>
        Directory.EnumerateFiles(root, "*.svg", SearchOption.AllDirectories)
            .Select(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var group = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
                return new StratagemIcon(ToId(name), name, group, path);
            })
            .OrderBy(icon => icon.Id, StringComparer.Ordinal)
            .ToList();
}
