// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellboardCompanion.App.Services;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Hellboard.Update;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Core.Recognition;

namespace HellboardCompanion.App.ViewModels;

/// <summary>
/// Overview tab: the chain game → recognition → Hellboard at a glance, with the stratagem icons
/// (implementation plan §6.7). Designed for demonstrations.
/// </summary>
public sealed partial class OverviewViewModel : ObservableObject, IDisposable
{
    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#4CAF50"));
    private static readonly IBrush Attention = new SolidColorBrush(Color.Parse("#FFC107"));
    private static readonly IBrush Inactive = new SolidColorBrush(Color.Parse("#808080"));
    private static readonly IBrush Problem = new SolidColorBrush(Color.Parse("#F44336"));

    private readonly IGameLocator? _game;
    private readonly LoadoutPipeline? _pipeline;
    private readonly HellboardLink? _hellboard;
    private readonly StratagemCatalog _catalog;
    private readonly StratagemImages _images;
    private readonly ManualTriggerService? _trigger;
    private readonly LockedLoadout? _previousSession;
    private readonly MultiFunctionOptions _multiFunction;
    private LoadoutSnapshot? _shownSnapshot;

    [ObservableProperty]
    private string _gameText = "–";

    [ObservableProperty]
    private IBrush _gameBrush = Inactive;

    [ObservableProperty]
    private string _recognitionText = "–";

    [ObservableProperty]
    private IBrush _recognitionBrush = Inactive;

    [ObservableProperty]
    private string _hellboardText = "–";

    [ObservableProperty]
    private IBrush _hellboardBrush = Inactive;

    [ObservableProperty]
    private string _loadoutTitle = "No loadout recognized yet";

    [ObservableProperty]
    private string _multiFunctionTitle = "Multi-function button";

    [ObservableProperty]
    private string? _warning;

    public OverviewViewModel(
        IGameLocator? game,
        LoadoutPipeline? pipeline,
        HellboardLink? hellboard,
        StratagemCatalog catalog,
        StratagemImages images,
        ManualTriggerService? trigger,
        HellboardStatusViewModel hellboardStatus,
        LoadoutHistory? history = null,
        MultiFunctionOptions? multiFunction = null)
    {
        _multiFunction = multiFunction ?? new MultiFunctionOptions();
        _previousSession = history?.ReadRecentLoadouts(1).FirstOrDefault();
        _game = game;
        _pipeline = pipeline;
        _hellboard = hellboard;
        _catalog = catalog;
        _images = images;
        _trigger = trigger;
        HellboardStatus = hellboardStatus;

        for (var i = 0; i < 4; i++)
        {
            Stratagems.Add(StratagemTileViewModel.Empty);
        }

        if (_game is not null)
        {
            _game.Changed += OnGameChanged;
        }

        if (_pipeline is not null)
        {
            _pipeline.ObservationAvailable += OnObservation;
            _pipeline.LoadoutLocked += OnLocked;
        }

        if (_hellboard is not null)
        {
            _hellboard.StateChanged += OnHellboardChanged;
        }

        Refresh();
    }

    public ObservableCollection<StratagemTileViewModel> Stratagems { get; } = [];

    public ObservableCollection<StratagemTileViewModel> MultiFunction { get; } = [];

    /// <summary>Connection details and the "Set as default" command.</summary>
    public HellboardStatusViewModel HellboardStatus { get; }

    public void Dispose()
    {
        if (_game is not null)
        {
            _game.Changed -= OnGameChanged;
        }

        if (_pipeline is not null)
        {
            _pipeline.ObservationAvailable -= OnObservation;
            _pipeline.LoadoutLocked -= OnLocked;
        }

        if (_hellboard is not null)
        {
            _hellboard.StateChanged -= OnHellboardChanged;
        }
    }

    [RelayCommand]
    private async Task RecognizeNowAsync()
    {
        if (_trigger is not null)
        {
            await _trigger.TriggerAsync();
        }
    }

    private void OnGameChanged(object? sender, GameSnapshot snapshot) => Dispatcher.UIThread.Post(Refresh);

    private void OnLocked(object? sender, LockedLoadout loadout) => Dispatcher.UIThread.Post(Refresh);

    private void OnHellboardChanged(object? sender, HellboardState state) => Dispatcher.UIThread.Post(Refresh);

    private void OnObservation(object? sender, LoadoutObservation observation)
    {
        // Up to 10 per second: only redraw when something visible changed.
        if (observation.Loadout is { } snapshot && !snapshot.Equals(_shownSnapshot))
        {
            Dispatcher.UIThread.Post(Refresh);
        }
        else if (observation.ScreenVisible != (_shownSnapshot is not null))
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    private void Refresh()
    {
        RefreshGame();
        RefreshLoadout();
        RefreshHellboard();
    }

    private void RefreshGame()
    {
        var snapshot = _game?.Current ?? GameSnapshot.NotRunning;
        (GameText, GameBrush) = snapshot.Status switch
        {
            GameStatus.Running when snapshot.Location!.IsMinimized => ("Helldivers 2 is minimized", Attention),
            GameStatus.Running => ($"Helldivers 2 runs on {snapshot.Location!.Monitor.DeviceName.TrimStart('\\', '.')} ({snapshot.Location.ClientBounds.Width}×{snapshot.Location.ClientBounds.Height})", Good),
            GameStatus.Starting => ("Helldivers 2 is starting …", Attention),
            _ => ("Helldivers 2 is not running", Inactive),
        };
    }

    private void RefreshLoadout()
    {
        var tracker = _pipeline?.Tracker;
        var observation = _pipeline?.LastObservation;

        (RecognitionText, RecognitionBrush) = tracker?.State switch
        {
            TrackerState.Selecting => ("Loadout screen – selecting", Attention),
            TrackerState.Locked => ("Loadout confirmed (Ready)", Good),
            _ when observation is null => ("Waiting for the game", Inactive),
            _ => ("Waiting for the loadout screen", Inactive),
        };

        // While the player selects, show what is on the screen; otherwise the last locked loadout.
        if (observation is { ScreenVisible: true, Loadout: { } live } && tracker?.State != TrackerState.Locked)
        {
            _shownSnapshot = live;
            LoadoutTitle = "On the loadout screen (not locked in yet)";
            SetTiles(live.PlayerSlots.Select(s => s.State == SlotState.Recognized ? s.StratagemId : s.State == SlotState.Unknown ? "?" : null));
        }
        else if (tracker?.LastLocked is { } locked)
        {
            _shownSnapshot = null;
            LoadoutTitle = $"Locked in at {locked.Timestamp.LocalDateTime:HH:mm:ss} ({Describe(locked.Source)})";
            SetTiles(locked.Stratagems);
        }
        else if (_previousSession is { } previous)
        {
            _shownSnapshot = null;
            LoadoutTitle = $"Last locked loadout from a previous session ({previous.Timestamp.LocalDateTime:g})";
            SetTiles(previous.Stratagems);
        }
        else
        {
            _shownSnapshot = null;
        }
    }

    private void RefreshHellboard()
    {
        var state = _hellboard?.State;
        var bundled = FirmwareVersions.EmbeddedVersion;
        var firmware = state is null ? FirmwareStatus.Unknown : FirmwareVersions.Classify(state, bundled);
        (HellboardText, HellboardBrush) = state?.Status switch
        {
            Core.Hellboard.HellboardStatus.Connected when state.Port == "Simulator" => ("Hellboard simulator", Attention),
            Core.Hellboard.HellboardStatus.Connected when firmware == FirmwareStatus.Outdated =>
                ($"Hellboard connected ({state.Port}) – firmware {state.Info?.Firmware} is outdated, {bundled} is available on the Hellboard tab", Attention),
            Core.Hellboard.HellboardStatus.Connected => ($"Hellboard connected ({state.Port})", Good),
            Core.Hellboard.HellboardStatus.NoProtocol when firmware != FirmwareStatus.Incompatible => ($"Hellboard on {state.Port} – {state.Message}", Attention),
            Core.Hellboard.HellboardStatus.NoProtocol when state.Info?.Protocol > HellboardClient.ProtocolVersion =>
                ("Hellboard firmware is newer than this Companion – please update Hellboard Companion", Problem),
            Core.Hellboard.HellboardStatus.NoProtocol => ("Hellboard firmware not compatible – install the firmware on the Hellboard tab", Problem),
            Core.Hellboard.HellboardStatus.Updating => ("Installing Hellboard firmware …", Attention),
            Core.Hellboard.HellboardStatus.DryRun => ("Hellboard: dry run", Attention),
            _ => ("No Hellboard connected", Inactive),
        };

        // Before anything was sent in this session, preview the list from the shown loadout.
        var multiFunction = state?.LastSent?.MissionStratagems
            ?? state?.Pending?.MissionStratagems
            ?? ((_pipeline?.Tracker.LastLocked ?? _previousSession) is { } shown
                ? LoadoutTransferBuilder.Build(shown, _catalog, _multiFunction).Loadout.MissionStratagems
                : null);
        MultiFunction.Clear();
        foreach (var definition in multiFunction ?? [])
        {
            var entry = _catalog.Entries.FirstOrDefault(e => e.Name == definition.Name);
            MultiFunction.Add(new StratagemTileViewModel(entry?.Id, definition.Name, _images.Get(entry?.Id)));
        }

        MultiFunctionTitle = MultiFunction.Count == 0 ? "Multi-function button – empty" : "Multi-function button";
        Warning = state?.NotTransferable.Count > 0
            ? $"Not transferable yet (no input sequence known): {string.Join(", ", state.NotTransferable.Select(id => _catalog.Find(id)?.Name ?? id))}"
            : null;
    }

    private void SetTiles(IEnumerable<string?> ids)
    {
        var index = 0;
        foreach (var id in ids.Take(4))
        {
            var tile = id switch
            {
                null => StratagemTileViewModel.Empty,
                "?" => new StratagemTileViewModel(null, "not recognized", null),
                _ => new StratagemTileViewModel(
                    id,
                    _catalog.Find(id)?.Name ?? id,
                    _images.Get(id),
                    _catalog.Find(id)?.IsTransferable == false ? "no input sequence yet" : null),
            };

            if (!Equals(Stratagems[index], tile))
            {
                Stratagems[index] = tile;
            }

            index++;
        }
    }

    private static string Describe(LockSource source) => source switch
    {
        LockSource.Ready => "Ready",
        LockSource.ScreenExit => "screen closed",
        _ => "manual",
    };
}
