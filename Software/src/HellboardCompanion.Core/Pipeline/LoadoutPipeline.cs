// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Recognition;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.Core.Pipeline;

/// <summary>
/// Background loop: capture → recognize → track (implementation plan §6.6). Runs slowly while no
/// loadout screen is visible (only the cheap screen check) and fast while it is visible, so the
/// lock-in does not miss a short Ready phase.
/// </summary>
public sealed class LoadoutPipeline : IDisposable
{
    /// <summary>Interval while no loadout screen is visible.</summary>
    public static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Interval while the loadout screen is visible.</summary>
    public static readonly TimeSpan ActiveInterval = TimeSpan.FromMilliseconds(100);

    private readonly GameFrameGrabber _grabber;
    private readonly StratagemIconStore _iconStore;
    private readonly LoadoutHistory _history;
    private readonly ILogger<LoadoutPipeline> _logger;
    private readonly CancellationTokenSource _stop = new();
    private LoadoutRecognizer? _recognizer;
    private volatile bool _iconsChanged = true;
    private Task? _loop;

    public LoadoutPipeline(GameFrameGrabber grabber, StratagemIconStore iconStore, LoadoutHistory history, ILogger<LoadoutPipeline> logger)
    {
        _grabber = grabber;
        _iconStore = iconStore;
        _history = history;
        _logger = logger;
        _iconStore.IconsChanged += (_, _) => _iconsChanged = true;
        Tracker.Locked += OnLocked;
    }

    public LoadoutTracker Tracker { get; } = new();

    /// <summary>Last analyzed frame (<c>null</c> while the game is not captured).</summary>
    public LoadoutObservation? LastObservation { get; private set; }

    /// <summary>Raised on the pipeline thread after every analyzed frame.</summary>
    public event EventHandler<LoadoutObservation>? ObservationAvailable;

    /// <summary>Raised on the pipeline thread when a loadout was locked in.</summary>
    public event EventHandler<LockedLoadout>? LoadoutLocked;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_stop.Token));

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
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan Step()
    {
        var recognizer = GetRecognizer();
        if (recognizer is null)
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
