// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Recognition;

namespace HellboardCompanion.App.ViewModels;

/// <summary>Live state of the loadout recognition for the status window.</summary>
public sealed partial class LoadoutStatusViewModel : ObservableObject, IDisposable
{
    private readonly LoadoutPipeline? _pipeline;
    private readonly StratagemIconStore? _iconStore;

    [ObservableProperty]
    private string _screen = "–";

    [ObservableProperty]
    private string _readyStatus = "–";

    [ObservableProperty]
    private string _playerStratagems = "–";

    [ObservableProperty]
    private string _missionStratagems = "–";

    [ObservableProperty]
    private string _trackerState = "–";

    [ObservableProperty]
    private string _lastLocked = "–";

    [ObservableProperty]
    private string _icons = "–";

    public LoadoutStatusViewModel(LoadoutPipeline? pipeline, StratagemIconStore? iconStore)
    {
        _pipeline = pipeline;
        _iconStore = iconStore;

        if (_pipeline is not null)
        {
            _pipeline.ObservationAvailable += OnObservation;
            _pipeline.LoadoutLocked += OnLocked;
            if (_pipeline.LastObservation is { } observation)
            {
                Apply(observation);
            }

            if (_pipeline.Tracker.LastLocked is { } locked)
            {
                LastLocked = Describe(locked);
            }
        }

        if (_iconStore is not null)
        {
            _iconStore.IconsChanged += OnIconsChanged;
            Icons = DescribeIcons();
        }
    }

    public void Dispose()
    {
        if (_pipeline is not null)
        {
            _pipeline.ObservationAvailable -= OnObservation;
            _pipeline.LoadoutLocked -= OnLocked;
        }

        if (_iconStore is not null)
        {
            _iconStore.IconsChanged -= OnIconsChanged;
        }
    }

    private void OnObservation(object? sender, LoadoutObservation observation) => Dispatcher.UIThread.Post(() => Apply(observation));

    private void OnLocked(object? sender, LockedLoadout loadout) => Dispatcher.UIThread.Post(() => LastLocked = Describe(loadout));

    private void OnIconsChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => Icons = DescribeIcons());

    private void Apply(LoadoutObservation observation)
    {
        Screen = observation.ScreenVisible
            ? $"Loadout screen (analyzed in {observation.Duration.TotalMilliseconds:F0} ms)"
            : "Other screen";
        ReadyStatus = observation.ScreenVisible ? observation.Status.ToString() : "–";
        PlayerStratagems = observation.Loadout is { } loadout ? string.Join(", ", loadout.PlayerSlots) : "–";
        MissionStratagems = observation.Loadout is { } l ? string.Join(", ", l.MissionSlots) : "–";
        TrackerState = _pipeline?.Tracker.State.ToString() ?? "–";
    }

    private string DescribeIcons() => _iconStore!.Icons.Count > 0
        ? $"{_iconStore.Icons.Count} stratagem icons"
        : "No stratagem icons yet (downloading…)";

    private static string Describe(LockedLoadout loadout) =>
        $"{loadout.Timestamp.LocalDateTime:HH:mm:ss} – {loadout}";
}
