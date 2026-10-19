// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HellboardCompanion.Core.Catalog;

/// <summary>One stratagem row of the Helldivers wiki (Cargo table "Stratagems", wiki-source-plan §2.1).</summary>
/// <param name="Page">Wiki page name = full designation, e.g. "B/FLAM-80 Cremator".</param>
/// <param name="Title">Display title; mostly equal to <paramref name="Page"/>.</param>
/// <param name="Type">Wiki type, e.g. "Support Weapon" or "Sentry".</param>
/// <param name="Code">Input code as U/D/L/R, <c>null</c> if the wiki has none yet.</param>
/// <param name="Image">Icon file, e.g. "Cremator Stratagem Icon Background.svg".</param>
public sealed record WikiStratagem(string Page, string Title, string Type, string? Code, string Image)
{
    /// <summary>Catalog category of <see cref="Type"/> (Sentry and Emplacement are "Defensive", Ship/Objective/Other "Mission").</summary>
    public string Category => Type switch
    {
        "Orbital" => "Orbital",
        "Eagle" => "Eagle",
        "Support Weapon" => "Support weapon",
        "Backpack" => "Backpack",
        "Vehicle" => "Vehicle",
        "Sentry" or "Emplacement" => "Defensive",
        _ => "Mission",
    };

    /// <summary>Name of the icon without suffix, e.g. "Cremator"; empty for a placeholder icon of an unreleased stratagem.</summary>
    public string IconName => Image.Contains("Fallback Icon", StringComparison.OrdinalIgnoreCase)
        ? string.Empty
        : WikiClient.IconSuffix().Replace(Image.Replace('_', ' '), string.Empty);
}

/// <summary>
/// Reads the stratagem table of helldivers.wiki.gg through the MediaWiki API (wiki-source-plan §2). The wiki
/// is the authoritative source of codes, designations and icons (decided 2026-10-19).
/// </summary>
public sealed partial class WikiClient(HttpClient http)
{
    public const string Api = "https://helldivers.wiki.gg/api.php";

    public const string StratagemsPage = "https://helldivers.wiki.gg/wiki/Stratagems";

    public static readonly string StratagemQuery = $"{Api}?action=cargoquery&format=json&limit=500&tables=Stratagems" +
        "&fields=_pageName=page,title,stratagem_code=code,stratagem_type=type,image";

    /// <summary>All stratagems of the wiki's stratagem table.</summary>
    /// <exception cref="HttpRequestException">The wiki is not reachable.</exception>
    /// <exception cref="InvalidDataException">The answer does not have the expected structure.</exception>
    public async Task<IReadOnlyList<WikiStratagem>> GetStratagemsAsync(CancellationToken cancellationToken = default) =>
        ParseStratagems(await http.GetStringAsync(StratagemQuery, cancellationToken));

    /// <summary>Parses the answer of <see cref="StratagemQuery"/>.</summary>
    /// <exception cref="InvalidDataException">Not the expected structure (the wiki changed its table).</exception>
    public static IReadOnlyList<WikiStratagem> ParseStratagems(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("cargoquery", out var rows))
            {
                var error = document.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("info", out var info) ? info.GetString() : null;
                throw new InvalidDataException($"No stratagem table in the answer of the wiki{(error is null ? string.Empty : $": {error}")}.");
            }

            var stratagems = rows.EnumerateArray()
                .Select(row => row.GetProperty("title"))
                .Select(row => new WikiStratagem(Text(row, "page"), Text(row, "title"), Text(row, "type"), ParseCode(Text(row, "code")), Text(row, "image")))
                // Rows without a type come from mission pages that repeat a stratagem box; the Stratagems page does not list them.
                .Where(s => s.Page.Length > 0 && s.Type.Length > 0)
                .ToList();

            // A stratagem table without a single code means the code format changed, not that all codes are gone.
            return stratagems.Count > 0 && stratagems.Any(s => s.Code is not null)
                ? stratagems
                : throw new InvalidDataException("The stratagem table of the wiki has no rows with a code.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"Unexpected answer of the wiki: {ex.Message}", ex);
        }
    }

    /// <summary>The code stored as a row of arrow images ([[File:Stratagem Arrow Down.svg|link=]] …) as U/D/L/R.</summary>
    public static string? ParseCode(string wikitext)
    {
        var code = string.Concat(ArrowPattern().Matches(wikitext).Select(m => m.Groups[1].Value[0]));
        return code.Length > 0 ? code : null;
    }

    private static string Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? WebUtility.HtmlDecode(value.GetString()!).Trim() : string.Empty;

    [GeneratedRegex("Stratagem Arrow (Up|Down|Left|Right)")]
    private static partial Regex ArrowPattern();

    [GeneratedRegex(@"\s*Stratagem Icon( Background)?\.(svg|png)$", RegexOptions.IgnoreCase)]
    internal static partial Regex IconSuffix();
}
