// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HellboardCompanion.Core.Hellboard.Update;

/// <param name="Target">Path on the board, e.g. "main.py".</param>
public sealed record FirmwareFile(string Target, byte[] Content)
{
    public string Sha256 => Convert.ToHexStringLower(SHA256.HashData(Content));
}

/// <summary>
/// The files installed on the Hellboard, described by <c>Firmware/manifest.json</c>: each entry maps a
/// source file (relative to the manifest, may point outside <c>Firmware/</c>, e.g. to
/// <c>../doc/stratagems.json</c>) to its path on the board (implementation plan §6.10, F11). The bundle
/// always contains <c>version.json</c> with the manifest version, which the firmware reports in <c>ping</c>.
/// </summary>
public sealed partial class FirmwareBundle(string version, IReadOnlyList<FirmwareFile> files, IReadOnlyList<string>? obsolete = null)
{
    public const string VersionFile = "version.json";
    public const string StratagemFile = "stratagems.json";
    private const string ResourcePrefix = "Firmware/";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Firmware version, e.g. "2.0.0".</summary>
    public string Version { get; } = version;

    public IReadOnlyList<FirmwareFile> Files { get; } = files;

    /// <summary>Files of older firmware versions that are removed from the board after the installation.</summary>
    public IReadOnlyList<string> Obsolete { get; } = obsolete ?? [];

    /// <summary>Bundle from a <c>Firmware/manifest.json</c> on disk (upload tool).</summary>
    /// <exception cref="FormatException">The manifest is invalid or a target name is not allowed.</exception>
    public static FirmwareBundle Load(string manifestPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        return Parse(File.ReadAllBytes(manifestPath), source => File.ReadAllBytes(Path.Combine(directory, source)));
    }

    /// <summary>
    /// Bundle shipped inside the Companion (the <c>Firmware/</c> files are embedded at build time). The
    /// stratagem list is generated from <paramref name="catalog"/>, so the board also gets the user's own
    /// stratagems and corrections (F11).
    /// </summary>
    public static FirmwareBundle LoadEmbedded(StratagemCatalog catalog)
    {
        var assembly = typeof(FirmwareBundle).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToDictionary(n => n[ResourcePrefix.Length..].Replace('\\', '/'), StringComparer.Ordinal);

        byte[] Read(string source)
        {
            if (!resources.TryGetValue(source.Replace('\\', '/'), out var name))
            {
                throw new FormatException($"Firmware file '{source}' is not embedded.");
            }

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        var bundle = Parse(Read("manifest.json"), source => source.EndsWith('/' + StratagemFile, StringComparison.Ordinal) || source == StratagemFile
            ? Encoding.UTF8.GetBytes(catalog.ToBoardJson())
            : Read(source));
        return bundle;
    }

    private static FirmwareBundle Parse(byte[] manifestJson, Func<string, byte[]> readSource)
    {
        Manifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(manifestJson, JsonOptions) ?? throw new FormatException("Empty manifest.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"Manifest is not valid JSON: {e.Message}", e);
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new FormatException("The manifest has no version.");
        }

        var files = new List<FirmwareFile>();
        foreach (var entry in manifest.Files ?? [])
        {
            if (entry.Source is null || entry.Target is null || !TargetPattern().IsMatch(entry.Target) || entry.Target == VersionFile)
            {
                throw new FormatException($"Invalid manifest entry '{entry.Source}' → '{entry.Target}'.");
            }

            files.Add(new FirmwareFile(entry.Target, readSource(entry.Source)));
        }

        if (files.Count == 0 || files.Select(f => f.Target).Distinct(StringComparer.Ordinal).Count() != files.Count)
        {
            throw new FormatException("The manifest lists no files or a target twice.");
        }

        var obsolete = manifest.Obsolete ?? [];
        if (obsolete.Any(name => !TargetPattern().IsMatch(name) || files.Any(f => f.Target == name)))
        {
            throw new FormatException("Invalid entry in 'obsolete'.");
        }

        // Written before main.py (the last file of the manifest), so a board never reports a version it does not have.
        var versionFile = new FirmwareFile(VersionFile, Encoding.UTF8.GetBytes(new JsonObject { ["version"] = manifest.Version }.ToJsonString()));
        files.Insert(Math.Max(0, files.Count - 1), versionFile);
        return new FirmwareBundle(manifest.Version, files, obsolete);
    }

    /// <summary>Plain file names on the board; no quotes or paths, so they can be embedded in Python code.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.\-]*$")]
    internal static partial Regex TargetPattern();

    private sealed record Manifest(string? Version, List<ManifestEntry>? Files, List<string>? Obsolete);

    private sealed record ManifestEntry(string? Source, string? Target);
}
