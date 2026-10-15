// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Recognition;
using Microsoft.Extensions.Logging;

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
    private LoadoutRecognizer? _recognizer;
    private volatile bool _iconsChanged = true;
    private Task? _loop;

    public LoadoutPipeline(
        GameFrameGrabber grabber,
        StratagemIconStore iconStore,
        LoadoutHistory history,
        RecognitionOptions options,
        IPlatformPaths paths,
        ILogger<LoadoutPipeline> logger)
    {
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
        Tracker.Process(observation);
        ObservationAvailable?.Invoke(this, observation);

        return observation.ScreenVisible ? ActiveInterval : IdleInterval;
    }

    private ManualRecognitionResult RunManualRecognition(LoadoutRecognizer recognizer)
    {
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
            return Failed("No image of the game (not running or minimized).");
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
        if (locked)
        {
            Tracker.LockManually(loadout, timestamp);
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
            _recognizer = new LoadoutRecognizer(LoadoutLayout.Default, _iconStore.Icons);
            previous?.Dispose();
            _logger.LogInformation("Loadout recognizer ready with {Count} stratagem templates", _iconStore.Icons.Count);
        }

        return _recognizer;
    }

    private void OnLocked(object? sender, LockedLoadout loadout)
    {
        _logger.LogInformation("Loadout locked: {Loadout}", loadout.ToString());
        _history.Append(loadout);
        LoadoutLocked?.Invoke(this, loadout);
    }
}
