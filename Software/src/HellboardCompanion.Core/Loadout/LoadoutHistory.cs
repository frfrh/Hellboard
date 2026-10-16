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

    /// <summary>The most recent entries, newest first; unreadable lines are skipped.</summary>
    public IReadOnlyList<LockedLoadout> ReadRecent(int maxCount = 100)
    {
        string[] lines;
        lock (_lock)
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            lines = File.ReadAllLines(FilePath);
        }

        var result = new List<LockedLoadout>();
        for (var i = lines.Length - 1; i >= 0 && result.Count < maxCount; i--)
        {
            try
            {
                if (JsonSerializer.Deserialize<LockedLoadout>(lines[i], JsonOptions) is { } entry)
                {
                    result.Add(entry);
                }
            }
            catch (JsonException)
            {
                // skip damaged lines
            }
        }

        return result;
    }

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
