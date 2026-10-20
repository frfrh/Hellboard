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
/// <c>entry</c> (older versions) are loadouts. Loadouts chosen by hand are written with a delay, so
/// several tiles set in a row give one entry (manual-selection plan §6).
/// </summary>
public sealed class LoadoutHistory(IPlatformPaths paths, TimeProvider? time = null)
{
    /// <summary>Time a loadout chosen by hand waits before it is written.</summary>
    public static readonly TimeSpan PendingDelay = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Lock _lock = new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private LockedLoadout? _pending;
    private DateTimeOffset _pendingDue;
    private LockedLoadout? _lastWritten;

    public string FilePath { get; } = Path.Combine(paths.DataDirectory, "loadouts.jsonl");

    /// <summary>Folder for the screenshots of problems.</summary>
    public string ProblemImageDirectory { get; } = Path.Combine(paths.DataDirectory, "problems");

    /// <summary>Raised after a problem was recorded.</summary>
    public event EventHandler<RecognitionProblem>? ProblemRecorded;

    /// <summary>The most recent entries of both kinds, newest first; unreadable lines are skipped.</summary>
    public IReadOnlyList<HistoryItem> ReadRecent(int maxCount = 200)
    {
        string[] lines;
        LockedLoadout? pending;
        lock (_lock)
        {
            pending = _pending;
            lines = File.Exists(FilePath) ? File.ReadAllLines(FilePath) : [];
        }

        var result = new List<HistoryItem>();
        if (pending is not null && maxCount > 0)
        {
            result.Add(new HistoryItem(pending, null));
        }

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

    /// <summary>Writes the loadout (after a pending one). The same record twice in a row is written once.</summary>
    public void Append(LockedLoadout loadout)
    {
        lock (_lock)
        {
            WritePending();
            WriteLoadout(loadout);
        }
    }

    public void Append(RecognitionProblem problem)
    {
        var node = JsonSerializer.SerializeToNode(problem, JsonOptions)!.AsObject();
        node.Insert(0, "entry", "problem");
        lock (_lock)
        {
            WritePending();
            WriteLine(node.ToJsonString());
        }

        ProblemRecorded?.Invoke(this, problem);
    }

    /// <summary>Keeps the loadout for <see cref="PendingDelay"/>; a further one in that time replaces it.</summary>
    public void AppendLater(LockedLoadout loadout)
    {
        lock (_lock)
        {
            _pending = loadout;
            _pendingDue = _time.GetUtcNow() + PendingDelay;
        }
    }

    /// <summary>Writes the pending loadout once its delay is over, or at once with <paramref name="force"/>.</summary>
    public void FlushPending(bool force = false)
    {
        lock (_lock)
        {
            if (force || _time.GetUtcNow() >= _pendingDue)
            {
                WritePending();
            }
        }
    }

    /// <summary>Drops the pending loadout without writing it; <c>true</c> if there was one.</summary>
    public bool DiscardPending()
    {
        lock (_lock)
        {
            var had = _pending is not null;
            _pending = null;
            return had;
        }
    }

    private void WritePending()
    {
        if (_pending is { } pending)
        {
            _pending = null;
            WriteLoadout(pending);
        }
    }

    private void WriteLoadout(LockedLoadout loadout)
    {
        if (ReferenceEquals(loadout, _lastWritten))
        {
            return;
        }

        var node = JsonSerializer.SerializeToNode(loadout, JsonOptions)!.AsObject();
        node.Insert(0, "entry", "loadout");
        WriteLine(node.ToJsonString());
        _lastWritten = loadout;
    }

    private void WriteLine(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.AppendAllText(FilePath, json + Environment.NewLine);
    }
}
