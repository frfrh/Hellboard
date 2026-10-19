// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;

namespace HellboardCompanion.Core.Catalog;

/// <summary>A stratagem icon (wiki-source-plan §4).</summary>
/// <param name="Id">Catalog id of the stratagem.</param>
/// <param name="Group">Wiki type (e.g. "Sentry"); folder of the former icon repository for its icons.</param>
/// <param name="ImagePath">SVG, or a PNG with transparency derived from a wiki icon with background.</param>
/// <param name="Recognizable">
/// <c>false</c> if several stratagems share the image (e.g. the drills): it is shown, but cannot tell them apart
/// in the recognition.
/// </param>
public sealed record StratagemIcon(string Id, string Name, string Group, string ImagePath, bool Recognizable = true)
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
}
