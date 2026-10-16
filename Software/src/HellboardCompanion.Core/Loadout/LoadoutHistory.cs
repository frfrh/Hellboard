// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Loadout;

public enum ProblemKind
{
    /// <summary>A slot is occupied, but the stratagem could not be recognized (e.g. a new stratagem).</summary>
    UnrecognizedStratagem,

    /// <summary>The stratagem was recognized, but its input sequence is not known yet.</summary>
    NoInputSequence,

    /// <summary>A manual recognition did not find the loadout screen.</summary>
    ScreenNotDetected,

    /// <summary>The recognition failed with an error.</summary>
    RecognitionError,
}

/// <param name="ImagePath">Screenshot of the affected area (PNG in the data directory), if any.</param>
/// <param name="StratagemId">Affected stratagem, if known.</param>
public sealed record RecognitionProblem(DateTimeOffset Timestamp, ProblemKind Kind, string Message, string? ImagePath, string? StratagemId);

/// <summary>One line of the history: a locked loadout or a problem.</summary>
public sealed record HistoryItem(LockedLoadout? Loadout, RecognitionProblem? Problem)
{
    public DateTimeOffset Timestamp => Loadout?.Timestamp ?? Problem!.Timestamp;
}

/// <summary>
/// Appends locked loadouts and recognition problems to <c>loadouts.jsonl</c> in the data directory
/// (implementation plan §6.5). Every line starts with <c>"entry": "loadout" | "problem"</c>; lines without
/// <c>entry</c> (older versions) are loadouts.
/// </summary>
public sealed class LoadoutHistory(IPlatformPaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Lock _lock = new();

    public string FilePath { get; } = Path.Combine(paths.DataDirectory, "loadouts.jsonl");

    /// <summary>Folder for the screenshots of problems.</summary>
    public string ProblemImageDirectory { get; } = Path.Combine(paths.DataDirectory, "problems");

    /// <summary>Raised after a problem was recorded.</summary>
    public event EventHandler<RecognitionProblem>? ProblemRecorded;

    /// <summary>The most recent entries of both kinds, newest first; unreadable lines are skipped.</summary>
    public IReadOnlyList<HistoryItem> ReadRecent(int maxCount = 200)
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

        var result = new List<HistoryItem>();
        for (var i = lines.Length - 1; i >= 0 && result.Count < maxCount; i--)
        {
            try
            {
                if (JsonNode.Parse(lines[i]) is not JsonObject node)
                {
                    continue;
                }

                if (node["entry"]?.GetValue<string>() == "problem")
                {
                    result.Add(new HistoryItem(null, node.Deserialize<RecognitionProblem>(JsonOptions)));
                }
                else if (node.Deserialize<LockedLoadout>(JsonOptions) is { } loadout)
                {
                    result.Add(new HistoryItem(loadout, null));
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                // skip damaged lines
            }
        }

        return result;
    }

    /// <summary>The most recent locked loadouts only, newest first.</summary>
    public IReadOnlyList<LockedLoadout> ReadRecentLoadouts(int maxCount = 100) =>
        ReadRecent(maxCount * 4).Select(i => i.Loadout).OfType<LockedLoadout>().Take(maxCount).ToList();

    public void Append(LockedLoadout loadout)
    {
        var node = JsonSerializer.SerializeToNode(loadout, JsonOptions)!.AsObject();
        node.Insert(0, "entry", "loadout");
        AppendLine(node.ToJsonString());
    }

    public void Append(RecognitionProblem problem)
    {
        var node = JsonSerializer.SerializeToNode(problem, JsonOptions)!.AsObject();
        node.Insert(0, "entry", "problem");
        AppendLine(node.ToJsonString());
        ProblemRecorded?.Invoke(this, problem);
    }

    private void AppendLine(string json)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, json + Environment.NewLine);
        }
    }
}
