// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;

namespace HellboardCompanion.Core.Updates;

/// <summary>A published Companion release on GitHub.</summary>
/// <param name="Url">Release page, opened in the browser to download it.</param>
public sealed record CompanionRelease(Version Version, string Tag, string Url)
{
    /// <summary>"0.8.0" – the version as shown to the user and stored as skipped version.</summary>
    public string VersionText => Version.ToString(3);
}

/// <summary>
/// Looks for a newer Companion release (decided 2026-10-19): the releases of <c>frfrh/Hellboard</c> are the
/// only GitHub query of the Companion, made at start (can be turned off). Releases of the Companion are tagged
/// <c>vX.Y.Z_companion</c> (older ones <c>vX.Y.Z</c>); other parts of the project get other tags, drafts and
/// pre-releases are ignored.
/// </summary>
public sealed partial class CompanionUpdateChecker(HttpClient http)
{
    public const string Repository = "frfrh/Hellboard";

    public static readonly string ReleasesApi = $"https://api.github.com/repos/{Repository}/releases?per_page=30";

    /// <summary>
    /// Test switch: the update check takes this version as the running one (e.g. <c>0.6.0</c>), so the dialog and
    /// the notification can be tried against a published release.
    /// </summary>
    public const string VersionOverrideVariable = "HELLBOARD_COMPANION_VERSION";

    /// <summary>The version of this build ("0.7.0" from "0.7.0+commit"), or the test switch <see cref="VersionOverrideVariable"/>.</summary>
    public static Version CurrentVersion { get; } = VersionOf(Environment.GetEnvironmentVariable(VersionOverrideVariable));

    /// <summary>Whether <see cref="CurrentVersion"/> comes from the test switch.</summary>
    public static bool IsVersionOverridden => CurrentVersion.ToString(3) != AppInfo.ShortVersion;

    /// <summary>The override if it is a version like "0.6.0", the version of this build otherwise.</summary>
    public static Version VersionOf(string? versionOverride) =>
        Version.TryParse(versionOverride, out var forced) && forced.Build >= 0 ? forced
        : Version.TryParse(AppInfo.ShortVersion, out var version) ? version
        : new Version(0, 0, 0);

    /// <summary>The newest published Companion release, <c>null</c> if there is none.</summary>
    /// <exception cref="HttpRequestException">GitHub is not reachable or refused the request (e.g. rate limit).</exception>
    /// <exception cref="InvalidDataException">The answer does not have the expected structure.</exception>
    public async Task<CompanionRelease?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApi);
        request.Headers.UserAgent.ParseAdd($"HellboardCompanion/{AppInfo.ShortVersion} (+https://github.com/{Repository})");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return ParseLatest(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <summary>A release newer than this build that the user did not skip, <c>null</c> otherwise.</summary>
    public async Task<CompanionRelease?> FindUpdateAsync(string? skippedVersion, CancellationToken cancellationToken = default) =>
        await GetLatestAsync(cancellationToken) is { } latest && IsOffered(latest, CurrentVersion, skippedVersion) ? latest : null;

    /// <summary>Newer than <paramref name="current"/> and not the version the user chose to skip (a later one is offered again).</summary>
    public static bool IsOffered(CompanionRelease release, Version current, string? skippedVersion) =>
        release.Version > current && release.VersionText != skippedVersion;

    /// <summary>The newest Companion release of the answer of <see cref="ReleasesApi"/>.</summary>
    /// <exception cref="InvalidDataException">Not the expected structure.</exception>
    public static CompanionRelease? ParseLatest(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.EnumerateArray()
                .Where(r => !r.GetProperty("draft").GetBoolean() && !r.GetProperty("prerelease").GetBoolean())
                .Select(r => (Tag: r.GetProperty("tag_name").GetString() ?? string.Empty, Url: r.GetProperty("html_url").GetString() ?? string.Empty))
                .Select(r => (r.Tag, r.Url, Match: TagPattern().Match(r.Tag)))
                .Where(r => r.Match.Success && r.Url.StartsWith("https://github.com/", StringComparison.Ordinal))
                .Select(r => new CompanionRelease(Version.Parse(r.Match.Groups[1].Value), r.Tag, r.Url))
                .MaxBy(r => r.Version);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"Unexpected answer of GitHub: {ex.Message}", ex);
        }
    }

    [GeneratedRegex(@"^v(\d+\.\d+\.\d+)(_companion)?$")]
    private static partial Regex TagPattern();
}
