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
    private readonly LoadoutHistory? _history;
    private LoadoutSnapshot? _shownSnapshot;

    // The shown loadout, the base of a choice by hand (manual-selection plan §2).
    private IReadOnlyList<string?> _shownIds = [null, null, null, null];
    private IReadOnlyList<bool>? _shownByHand;
    private IReadOnlyList<string?>? _shownRecognized;
    private IReadOnlyList<string> _shownMission = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameBrush))]
    private StatusLine _gameLine = new("–", null, StatusLevel.Inactive);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecognitionBrush))]
    private StatusLine _recognitionLine = new("–", null, StatusLevel.Inactive);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HellboardBrush))]
    private StatusLine _hellboardLine = new("–", null, StatusLevel.Inactive);

    /// <summary>The game runs (also in the background or minimized): the recognize button is shown.</summary>
    [ObservableProperty]
    private bool _isGameRunning;

    /// <summary>The game window can be captured (running, not minimized).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecognizeNowCommand))]
    private bool _canRecognize;

    [ObservableProperty]
    private string _loadoutTitle = "No loadout recognized yet";

    [ObservableProperty]
    private string _multiFunctionTitle = "Multi-function button";

    [ObservableProperty]
    private string? _warning;

    /// <summary>The shown loadout contains hand choices and there is a recognized loadout to go back to.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackToRecognizedCommand))]
    private bool _canGoBackToRecognized;

    /// <summary>Nothing was recognized in this session: hint that the tiles can be chosen by hand.</summary>
    [ObservableProperty]
    private bool _showHandHint;

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
        _history = history;
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
            Stratagems.Add(new LoadoutTileViewModel(i, StratagemTileViewModel.Empty));
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

    public ObservableCollection<LoadoutTileViewModel> Stratagems { get; } = [];

    /// <summary>Multi-function list in board order, in its groups "Fixed list" and "Detected".</summary>
    public ObservableCollection<MultiFunctionGroupViewModel> MultiFunctionGroups { get; } = [];

    /// <summary>Connection details and the "Set as default" command.</summary>
    public HellboardStatusViewModel HellboardStatus { get; }

    public IBrush GameBrush => BrushOf(GameLine.Level);

    public IBrush RecognitionBrush => BrushOf(RecognitionLine.Level);

    public IBrush HellboardBrush => BrushOf(HellboardLine.Level);

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

    [RelayCommand(CanExecute = nameof(CanRecognize))]
    private async Task RecognizeNowAsync()
    {
        if (_trigger is not null)
        {
            await _trigger.TriggerAsync();
        }
    }

    /// <summary>The picker for a tile; choosing in it sends the changed loadout like a recognized one.</summary>
    public StratagemPickerViewModel CreatePicker(int slot)
    {
        HandChoice.TryGetRecognized(_shownByHand, _shownRecognized, slot, out var recognized);
        var recent = HandChoice.RecentlyUsed(_history?.ReadRecentLoadouts(20) ?? [], _catalog.MissionOnlyIds());
        var shown = _shownIds.Select(id => id == HandChoice.NotRecognized ? null : id).ToList();
        var picker = new StratagemPickerViewModel(_catalog, _images, shown, slot, recognized, recent);
        picker.Chosen += (_, id) => ChooseByHand(slot, id);
        return picker;
    }

    private void ChooseByHand(int slot, string? id)
    {
        var loadout = HandChoice.Apply(_shownIds, _shownByHand, _shownMission, slot, id, DateTimeOffset.Now, _shownRecognized);
        _pipeline?.SetByHandAsync(loadout);
    }

    /// <summary>Right-click on a tile: sets a slot chosen by hand back to what the recognition had there.</summary>
    public void ResetToRecognized(int slot)
    {
        if (HandChoice.Reset(_shownIds, _shownByHand, _shownRecognized, _shownMission, slot, DateTimeOffset.Now) is { } loadout)
        {
            _pipeline?.SetByHandAsync(loadout);
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBackToRecognized))]
    private async Task BackToRecognizedAsync()
    {
        if (_pipeline is not null)
        {
            await _pipeline.RevertToRecognizedAsync();
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
        GameLine = OverviewStatus.Game(snapshot);
        IsGameRunning = snapshot.Status == GameStatus.Running;
        CanRecognize = snapshot is { Status: GameStatus.Running, Location.IsMinimized: false };
    }

    private void RefreshLoadout()
    {
        var tracker = _pipeline?.Tracker;
        var observation = _pipeline?.LastObservation;

        RecognitionLine = OverviewStatus.Recognition(tracker?.State, tracker?.LastLocked?.Source, observation is not null);
        var live = observation is { ScreenVisible: true, Loadout: { } snapshot } && tracker?.State != TrackerState.Locked ? snapshot : null;
        ShowHandHint = tracker?.LastRecognized is null && live is null;
        CanGoBackToRecognized = tracker is { LastLocked.Source: LockSource.ByHand, LastRecognized: not null };

        // While the player selects, show what is on the screen – unless a hand choice corrected exactly that;
        // otherwise the last locked loadout.
        if (live is not null && tracker?.IsCorrectedByHand(live) != true)
        {
            _shownSnapshot = live;
            LoadoutTitle = "On the loadout screen (not locked in yet)";
            Show(
                live.PlayerSlots.Select(s => s.State == SlotState.Recognized ? s.StratagemId : s.State == SlotState.Unknown ? HandChoice.NotRecognized : null).ToList(),
                null,
                null,
                live.MissionSlots.Where(s => s.State == SlotState.Recognized).Select(s => s.StratagemId!).ToList());
        }
        else if (tracker?.LastLocked is { } locked)
        {
            _shownSnapshot = live;
            LoadoutTitle = OverviewStatus.LockedTitle(locked, tracker.LastRecognized);
            Show(locked.Stratagems, locked.ByHand, locked.Recognized, locked.MissionStratagems);
        }
        else if (_previousSession is { } previous)
        {
            _shownSnapshot = null;
            LoadoutTitle = $"Last locked loadout from a previous session ({previous.Timestamp.LocalDateTime:g})";
            Show(previous.Stratagems, previous.ByHand, previous.Recognized, previous.MissionStratagems);
        }
        else
        {
            _shownSnapshot = null;
            Show([null, null, null, null], null, null, []);
        }
    }

    private void Show(IReadOnlyList<string?> ids, IReadOnlyList<bool>? byHand, IReadOnlyList<string?>? recognized, IReadOnlyList<string> mission)
    {
        _shownIds = ids;
        _shownByHand = byHand;
        _shownRecognized = recognized;
        _shownMission = mission;
        SetTiles(ids, byHand, recognized);
    }

    private void RefreshHellboard()
    {
        var state = _hellboard?.State;
        HellboardLine = OverviewStatus.Hellboard(state, FirmwareVersions.EmbeddedVersion);

        // Before anything was sent in this session, preview the list from the shown loadout.
        var multiFunction = state?.LastSent?.MissionStratagems
            ?? state?.Pending?.MissionStratagems
            ?? ((_pipeline?.Tracker.LastLocked ?? _previousSession) is { } shown
                ? LoadoutTransferBuilder.Build(shown, _catalog, _multiFunction).Loadout.MissionStratagems
                : null);
        var tiles = (multiFunction ?? []).Select(definition =>
        {
            var entry = _catalog.Entries.FirstOrDefault(e => e.Name == definition.Name);
            return new StratagemTileViewModel(entry?.Id, definition.Name, _images.Get(entry?.Id));
        }).ToList();
        SetMultiFunction(tiles);
        Warning = state?.NotTransferable.Count > 0
            ? $"Not transferable yet (no input sequence known): {string.Join(", ", state.NotTransferable.Select(id => _catalog.Find(id)?.Name ?? id))}"
            : null;
    }

    /// <summary>Shows the multi-function list, grouped by origin (also used by the screenshot tool).</summary>
    public void SetMultiFunction(IReadOnlyList<StratagemTileViewModel> tiles)
    {
        MultiFunctionGroups.Clear();
        foreach (var (title, entries) in ViewModels.MultiFunctionGroups.Split(tiles, t => t.Id, _multiFunction))
        {
            MultiFunctionGroups.Add(new MultiFunctionGroupViewModel(title, entries));
        }

        MultiFunctionTitle = tiles.Count == 0 ? "Multi-function button – empty" : "Multi-function button";
    }

    private void SetTiles(IEnumerable<string?> ids, IReadOnlyList<bool>? byHand = null, IReadOnlyList<string?>? recognized = null)
    {
        var index = 0;
        foreach (var id in ids.Take(4))
        {
            var content = id switch
            {
                null => StratagemTileViewModel.Empty,
                HandChoice.NotRecognized => new StratagemTileViewModel(null, "not recognized", null),
                _ => new StratagemTileViewModel(
                    id,
                    _catalog.Find(id)?.Name ?? id,
                    _images.Get(id),
                    _catalog.Find(id)?.IsTransferable == false ? "no input sequence yet" : null),
            };
            var resetTo = HandChoice.TryGetRecognized(byHand, recognized, index, out var recognizedId)
                ? recognizedId is null ? "empty" : _catalog.Find(recognizedId)?.Name ?? recognizedId
                : null;
            var tile = new LoadoutTileViewModel(index, content, byHand is not null && index < byHand.Count && byHand[index], ShowHandHint && id is null, resetTo);

            if (!Equals(Stratagems[index], tile))
            {
                Stratagems[index] = tile;
            }

            index++;
        }
    }

    private static IBrush BrushOf(StatusLevel level) => level switch
    {
        StatusLevel.Good => Good,
        StatusLevel.Attention => Attention,
        StatusLevel.Problem => Problem,
        _ => Inactive,
    };
}
