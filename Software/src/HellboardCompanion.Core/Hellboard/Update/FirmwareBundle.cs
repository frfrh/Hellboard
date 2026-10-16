// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;
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
/// <c>../doc/stratagems.json</c>) to its path on the board (implementation plan §6.10, F11).
/// </summary>
public sealed partial class FirmwareBundle(IReadOnlyList<FirmwareFile> files)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<FirmwareFile> Files { get; } = files;

    /// <exception cref="FormatException">The manifest is invalid or a target name is not allowed.</exception>
    public static FirmwareBundle Load(string manifestPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        Manifest manifest;
        try
        {
            using var stream = File.OpenRead(manifestPath);
            manifest = JsonSerializer.Deserialize<Manifest>(stream, JsonOptions) ?? throw new FormatException("Empty manifest.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"Manifest is not valid JSON: {e.Message}", e);
        }

        var files = new List<FirmwareFile>();
        foreach (var entry in manifest.Files ?? [])
        {
            if (entry.Source is null || entry.Target is null || !TargetPattern().IsMatch(entry.Target))
            {
                throw new FormatException($"Invalid manifest entry '{entry.Source}' → '{entry.Target}'.");
            }

            files.Add(new FirmwareFile(entry.Target, File.ReadAllBytes(Path.Combine(directory, entry.Source))));
        }

        if (files.Count == 0 || files.Select(f => f.Target).Distinct(StringComparer.Ordinal).Count() != files.Count)
        {
            throw new FormatException("The manifest lists no files or a target twice.");
        }

        return new FirmwareBundle(files);
    }

    /// <summary>Plain file names on the board; no quotes or paths, so they can be embedded in Python code.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.\-]*$")]
    internal static partial Regex TargetPattern();

    private sealed record Manifest(List<ManifestEntry>? Files);

    private sealed record ManifestEntry(string? Source, string? Target);
}
