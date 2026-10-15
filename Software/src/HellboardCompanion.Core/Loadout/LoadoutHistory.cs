// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Loadout;

/// <summary>Appends every locked loadout to <c>loadouts.jsonl</c> in the data directory (implementation plan §6.5).</summary>
public sealed class LoadoutHistory(IPlatformPaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Lock _lock = new();

    public string FilePath { get; } = Path.Combine(paths.DataDirectory, "loadouts.jsonl");

    public void Append(LockedLoadout loadout)
    {
        var line = JsonSerializer.Serialize(loadout, JsonOptions) + Environment.NewLine;
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, line);
        }
    }
}
