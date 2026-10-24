// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Recognition;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace HellboardCompanion.Core.Pipeline;

/// <summary>
/// Background loop: capture → recognize → track (implementation plan §6.6). Runs slowly while no
/// loadout screen is visible (only the cheap screen check) and fast while it is visible, so the
/// lock-in does not miss a short Ready phase. Manual triggers and file analyses (§6.13) are executed
/// on the same thread, so recognizer and tracker are never used concurrently.
/// </summary>
public sealed class LoadoutPipeline : IDisposable
{
    /// <summary>Interval while no loadout screen is visible.</summary>
    public static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Interval while the loadout screen is visible.</summary>
    public static readonly TimeSpan ActiveInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>Frames captured per manual trigger, and the time between them.</summary>
    public const int BurstFrames = 5;
    public static readonly TimeSpan BurstSpacing = TimeSpan.FromMilliseconds(40);

    private readonly GameFrameGrabber _grabber;
    private readonly StratagemIconStore _iconStore;
    private readonly LoadoutHistory _history;
    private readonly RecognitionOptions _options;
    private readonly IPlatformPaths _paths;
    private readonly ILogger<LoadoutPipeline> _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentQueue<Action<LoadoutRecognizer?>> _requests = new();
    private readonly SemaphoreSlim _wake = new(0);
    /// <summary>Recognition errors are recorded in the history at most this often (per error type).</summary>
    private static readonly TimeSpan ErrorReportInterval = TimeSpan.FromMinutes(10);

    private readonly StratagemCatalog? _catalog;
    private readonly StratagemReferenceStore? _references;
    private readonly HashSet<string> _reportedWithoutSequence = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastErrorReport = new(StringComparer.Ordinal);
    private LoadoutRecognizer? _recognizer;
    private Frame? _lastLoadoutFrame;
    private LoadoutObservation? _lastLoadoutObservation;
    private volatile bool _iconsChanged = true;
    private Task? _loop;

    public LoadoutPipeline(
        GameFrameGrabber grabber,
        StratagemIconStore iconStore,
        LoadoutHistory history,
        RecognitionOptions options,
        IPlatformPaths paths,
        ILogger<LoadoutPipeline> logger,
        StratagemCatalog? catalog = null,
        StratagemReferenceStore? references = null)
    {
        _catalog = catalog;
        _references = references;
        _grabber = grabber;
        _iconStore = iconStore;
        _history = history;
        _options = options;
        _paths = paths;
        _logger = logger;
        _iconStore.IconsChanged += (_, _) =>
        {
            _iconsChanged = true;
            _wake.Release();
        };
        if (_catalog is not null)
        {
            // The categories decide which icons are compared in the player slots.
            _catalog.Changed += (_, _) => _iconsChanged = true;
        }

        if (_references is not null)
        {
            _references.Changed += (_, _) =>
            {
                _iconsChanged = true;
                _wake.Release();
            };
        }

        Tracker.Locked += OnLocked;
    }

    public LoadoutTracker Tracker { get; } = new();

    /// <summary>Last analyzed frame of the automatic loop (<c>null</c> while the game is not captured).</summary>
    public LoadoutObservation? LastObservation { get; private set; }

    /// <summary>Raised on the pipeline thread after every automatically analyzed frame.</summary>
    public event EventHandler<LoadoutObservation>? ObservationAvailable;

    /// <summary>Raised on the pipeline thread when a loadout was locked in.</summary>
    public event EventHandler<LockedLoadout>? LoadoutLocked;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_stop.Token));

    /// <summary>
    /// Captures a short burst of the game, recognizes it by majority vote and – unless in test mode –
    /// locks the result in (implementation plan §6.13).
    /// </summary>
    public Task<ManualRecognitionResult> RecognizeManuallyAsync() =>
        Enqueue(recognizer => recognizer is null
            ? Failed("The stratagem icons are not available yet.")
            : RunManualRecognition(recognizer));

    /// <summary>
    /// Locks in a loadout chosen by hand in the overview (manual-selection plan §3). Works without the
    /// game and in every recognition mode; history, problem entries and the transfer run as for a recognized loadout.
    /// A loadout without any slot set by hand (every slot reset by right-click) that equals the last recognized one
    /// counts as "Back to recognized".
    /// </summary>
    public Task SetByHandAsync(LockedLoadout loadout) =>
        Enqueue(_ =>
        {
            if (loadout.ByHand is null && Tracker.LastLocked?.Source == LockSource.ByHand && loadout.HasSameContent(Tracker.LastRecognized))
            {
                _history.DiscardPending();
                return Tracker.RevertToRecognized();
            }

            Tracker.LockByHand(loadout);
            return true;
        });

    /// <summary>Drops the hand choice and sends the last recognized loadout again; <c>false</c> if there is none.</summary>
    public Task<bool> RevertToRecognizedAsync() =>
        Enqueue(_ =>
        {
            // A hand choice not written yet disappears from the history as well.
            _history.DiscardPending();
            return Tracker.RevertToRecognized();
        });

    /// <summary>Runs the recognition on an image file (whole image = game area), without locking anything in.</summary>
    public Task<LoadoutObservation> AnalyzeFileAsync(string path) =>
        Enqueue(recognizer => recognizer is null
            ? throw new InvalidOperationException("The stratagem icons are not available yet.")
            : recognizer.Recognize(FrameFile.Load(path), force: true));

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _history.FlushPending(force: true);
        _recognizer?.Dispose();
        _stop.Dispose();
        _wake.Dispose();
    }

    private Task<T> Enqueue<T>(Func<LoadoutRecognizer?, T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Enqueue(recognizer =>
        {
            try
            {
                completion.SetResult(work(recognizer));
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        _wake.Release();
        return completion.Task;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var started = Stopwatch.GetTimestamp();
            var interval = IdleInterval;
            try
            {
                interval = Step();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loadout recognition failed");
                ReportError(ex);
            }

            var remaining = interval - Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero)
            {
                // Requests (manual trigger, file analysis) wake the loop immediately.
                await _wake.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan Step()
    {
        var recognizer = GetRecognizer();
        while (_requests.TryDequeue(out var request))
        {
            request(recognizer);
        }

        _history.FlushPending();

        if (recognizer is null || _options.Mode == RecognitionMode.ManualOnly)
        {
            return IdleInterval;
        }

        var frame = _grabber.Capture();
        if (frame is null)
        {
            LastObservation = null;
            return IdleInterval;
        }

        var observation = recognizer.Recognize(frame);
        LastObservation = observation;
        if (observation is { ScreenVisible: true, Loadout: not null })
        {
            // Kept for screenshots of unrecognized slots, also when the lock-in happens after the screen closed.
            _lastLoadoutFrame = frame;
            _lastLoadoutObservation = observation;
        }

        Tracker.Process(observation);
        ObservationAvailable?.Invoke(this, observation);

        return observation.ScreenVisible ? ActiveInterval : IdleInterval;
    }

    private ManualRecognitionResult RunManualRecognition(LoadoutRecognizer recognizer)
    {
        if (_grabber.Game.Status != GameStatus.Running)
        {
            return Failed(ManualRecognitionResult.GameNotRunning);
        }

        var timestamp = DateTimeOffset.Now;
        var frames = new List<Frame>();
        for (var i = 0; i < BurstFrames; i++)
        {
            if (_grabber.Capture() is { } frame)
            {
                frames.Add(frame);
            }

            if (i < BurstFrames - 1)
            {
                Thread.Sleep(BurstSpacing);
            }
        }

        if (frames.Count == 0)
        {
            return Failed("No image of the game (minimized?).");
        }

        var saved = _options.SaveSnapshotOnTrigger ? SaveFrames(frames, timestamp) : [];
        var observations = frames.Select(f => recognizer.Recognize(f, force: true)).ToList();
        var (loadout, status) = ManualRecognitionResult.Majority(observations);
        var screenDetected = observations.Count(o => o.ScreenVisible) * 2 > observations.Count;
        var screen = observations[^1].Screen;

        string? problem = null;
        if (!screenDetected)
        {
            problem = "Loadout screen not detected – the result may be wrong.";
        }
        else if (loadout.PlayerSlots.Any(s => s.State == SlotState.Unknown))
        {
            problem = "Not all stratagems were recognized.";
        }

        var testMode = !_options.ManualTransfer;
        var locked = !testMode && screenDetected && problem is null;
        _lastLoadoutFrame = frames[^1];
        _lastLoadoutObservation = observations[^1] with { Loadout = loadout };
        if (locked)
        {
            Tracker.LockManually(loadout, timestamp);
        }
        else if (!screenDetected)
        {
            RecordProblem(ProblemKind.ScreenNotDetected, "Manual recognition: the loadout screen was not detected.", SaveOverview(frames[^1], timestamp), null);
        }
        else if (problem is not null)
        {
            ReportUnrecognizedSlots(recognizer, timestamp);
        }

        _logger.LogInformation(
            "Manual recognition: {Loadout}, status {Status}, screen detected {Detected}, locked {Locked}, test mode {TestMode}, problem {Problem}",
            loadout.ToString(), status, screenDetected, locked, testMode, problem);
        return new ManualRecognitionResult(timestamp, frames.Count, screenDetected, status, loadout, screen, locked, testMode, saved, problem);
    }

    private ManualRecognitionResult Failed(string problem) =>
        new(DateTimeOffset.Now, 0, false, ReadyStatus.Unknown, null, null, false, !_options.ManualTransfer, [], problem);

    private List<string> SaveFrames(List<Frame> frames, DateTimeOffset timestamp)
    {
        var directory = Path.Combine(_paths.DataDirectory, "snapshots");
        var files = new List<string>();
        for (var i = 0; i < frames.Count; i++)
        {
            var path = Path.Combine(directory, $"trigger-{timestamp:yyyyMMdd-HHmmss}-{i + 1}-{frames[i].Width}x{frames[i].Height}.png");
            FrameFile.Save(frames[i], path);
            files.Add(path);
        }

        return files;
    }

    private LoadoutRecognizer? GetRecognizer()
    {
        if (_iconsChanged && _iconStore.Icons.Count > 0)
        {
            _iconsChanged = false;
            var previous = _recognizer;
            var references = _references?.References ?? [];
            _recognizer = new LoadoutRecognizer(LoadoutLayout.Default, _iconStore.Icons, _catalog?.MissionOnlyIds(), references);
            previous?.Dispose();
            _logger.LogInformation("Loadout recognizer ready with {Count} stratagem templates and {References} reference images", _iconStore.Icons.Count, references.Count);
        }

        return _recognizer;
    }

    private void OnLocked(object? sender, LockedLoadout loadout)
    {
        _logger.LogInformation("Loadout locked: {Loadout}", loadout.ToString());
        if (loadout.Source == LockSource.ByHand)
        {
            _history.AppendLater(loadout);
        }
        else
        {
            _history.Append(loadout);
        }

        try
        {
            if (!loadout.IsComplete && loadout.Source != LockSource.ByHand && _recognizer is not null)
            {
                ReportUnrecognizedSlots(_recognizer, loadout.Timestamp);
            }

            ReportMissingSequences(loadout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recording the recognition problems failed");
        }

        LoadoutLocked?.Invoke(this, loadout);
    }

    // ---- Problems for the history (shown in the "important" view) ------------------------

    /// <summary>A screenshot of every slot that is occupied but not recognized.</summary>
    private void ReportUnrecognizedSlots(LoadoutRecognizer recognizer, DateTimeOffset timestamp)
    {
        if (_lastLoadoutFrame is not { } frame || _lastLoadoutObservation is not { Loadout: { } snapshot } observation)
        {
            return;
        }

        void Report(IReadOnlyList<SlotValue> slots, bool mission, string label)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].State == SlotState.Unknown)
                {
                    var rect = recognizer.GetSlotRect(frame, observation.Screen.Offset, mission, i);
                    var image = SaveCrop(frame, rect, timestamp, $"{(mission ? "mission" : "slot")}{i + 1}");
                    RecordProblem(ProblemKind.UnrecognizedStratagem, $"{label} {i + 1}: stratagem not recognized (unknown or new icon).", image, null);
                }
            }
        }

        Report(snapshot.PlayerSlots, false, "Slot");
        Report(snapshot.MissionSlots, true, "Mission slot");
    }

    /// <summary>Recognized stratagems that cannot be sent because their input sequence is unknown (once per session).</summary>
    private void ReportMissingSequences(LockedLoadout loadout)
    {
        if (_catalog is null)
        {
            return;
        }

        foreach (var id in loadout.Stratagems.OfType<string>().Concat(loadout.MissionStratagems))
        {
            if (_catalog.Find(id) is { IsTransferable: true } || !_reportedWithoutSequence.Add(id))
            {
                continue;
            }

            var name = _catalog.Find(id)?.FullName ?? id;
            RecordProblem(ProblemKind.NoInputSequence, $"{name}: input sequence not known yet – not sent to the Hellboard.", null, id);
        }
    }

    private void ReportError(Exception error)
    {
        var key = error.GetType().FullName ?? "error";
        var now = DateTimeOffset.Now;
        if (_lastErrorReport.TryGetValue(key, out var last) && now - last < ErrorReportInterval)
        {
            return;
        }

        _lastErrorReport[key] = now;
        RecordProblem(ProblemKind.RecognitionError, $"Recognition error: {error.Message}", null, null);
    }

    private void RecordProblem(ProblemKind kind, string message, string? imagePath, string? stratagemId)
    {
        try
        {
            _history.Append(new RecognitionProblem(DateTimeOffset.Now, kind, message, imagePath, stratagemId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Writing the problem to the history failed");
        }
    }

    /// <summary>Saves the slot area (with a small margin) as PNG for the history.</summary>
    private string? SaveCrop(Frame frame, PixelRect slot, DateTimeOffset timestamp, string suffix)
    {
        var margin = slot.Width / 8;
        var area = new PixelRect(slot.X - margin, slot.Y - margin, slot.Width + (2 * margin), slot.Height + (2 * margin));
        using var crop = FrameMat.CropBgr(frame, area);
        return SaveImage(crop, timestamp, suffix);
    }

    /// <summary>Saves the whole frame, reduced to at most 960 px width.</summary>
    private string? SaveOverview(Frame frame, DateTimeOffset timestamp)
    {
        using var full = FrameMat.CropBgr(frame, new PixelRect(0, 0, frame.Width, frame.Height));
        using var small = new Mat();
        var scale = Math.Min(1.0, 960.0 / frame.Width);
        Cv2.Resize(full, small, new Size((int)(frame.Width * scale), (int)(frame.Height * scale)), interpolation: InterpolationFlags.Area);
        return SaveImage(small, timestamp, "screen");
    }

    private string? SaveImage(Mat image, DateTimeOffset timestamp, string suffix)
    {
        try
        {
            Directory.CreateDirectory(_history.ProblemImageDirectory);
            var path = Path.Combine(_history.ProblemImageDirectory, $"problem-{timestamp:yyyyMMdd-HHmmss-fff}-{suffix}.png");
            return Cv2.ImWrite(path, image) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or OpenCVException)
        {
            _logger.LogWarning(ex, "Saving the problem screenshot failed");
            return null;
        }
    }
}
