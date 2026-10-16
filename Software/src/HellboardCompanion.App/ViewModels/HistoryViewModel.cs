// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using HellboardCompanion.App.Services;
using HellboardCompanion.Core.Configuration;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Pipeline;

namespace HellboardCompanion.App.ViewModels;

/// <summary>One line of the history: a locked loadout or a recognition problem.</summary>
public sealed record HistoryEntryViewModel(
    string Time,
    string Title,
    IReadOnlyList<StratagemTileViewModel> Stratagems,
    string MissionStratagems,
    string? ProblemText = null,
    Bitmap? ProblemImage = null,
    StratagemTileViewModel? ProblemStratagem = null)
{
    public bool IsProblem => ProblemText is not null;

    public bool IsLoadout => !IsProblem;

    public bool HasProblemImage => ProblemImage is not null;

    public bool HasProblemStratagem => ProblemStratagem is not null;
}

/// <summary>
/// History tab (implementation plan §6.7). Default view "important": loadouts confirmed with Ready or
/// manually, and recognition problems with a screenshot of the affected slot. The selected view is saved.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject, IDisposable
{
    private readonly LoadoutHistory? _history;
    private readonly LoadoutPipeline? _pipeline;
    private readonly StratagemCatalog _catalog;
    private readonly StratagemImages _images;
    private readonly SettingsStore? _settings;

    public HistoryViewModel(LoadoutHistory? history, LoadoutPipeline? pipeline, StratagemCatalog catalog, StratagemImages images, SettingsStore? settings = null)
    {
        _history = history;
        _pipeline = pipeline;
        _catalog = catalog;
        _images = images;
        _settings = settings;
        if (_pipeline is not null)
        {
            _pipeline.LoadoutLocked += OnChanged;
        }

        if (_history is not null)
        {
            _history.ProblemRecorded += OnChanged;
        }

        Reload();
    }

    public IReadOnlyList<Choice<HistoryFilter>> Filters { get; } =
    [
        new(HistoryFilter.Important, "Important (Ready, manual, problems)"),
        new(HistoryFilter.All, "Everything"),
    ];

    public Choice<HistoryFilter> Filter
    {
        get => Filters.First(f => f.Value == (_settings?.Settings.Ui.HistoryFilter ?? HistoryFilter.Important));
        set
        {
            if (_settings is null || _settings.Settings.Ui.HistoryFilter == value.Value)
            {
                return;
            }

            _settings.Settings.Ui.HistoryFilter = value.Value;
            _settings.Save();
            OnPropertyChanged();
            Reload();
        }
    }

    public ObservableCollection<HistoryEntryViewModel> Entries { get; } = [];

    public string? FilePath => _history?.FilePath;

    public void Dispose()
    {
        if (_pipeline is not null)
        {
            _pipeline.LoadoutLocked -= OnChanged;
        }

        if (_history is not null)
        {
            _history.ProblemRecorded -= OnChanged;
        }
    }

    private void OnChanged<T>(object? sender, T e) => Dispatcher.UIThread.Post(Reload);

    private void Reload()
    {
        var showAll = Filter.Value == HistoryFilter.All;
        Entries.Clear();
        foreach (var item in _history?.ReadRecent() ?? [])
        {
            if (item.Loadout is { } loadout && (showAll || loadout.Source != LockSource.ScreenExit))
            {
                Entries.Add(ToEntry(loadout));
            }
            else if (item.Problem is { } problem)
            {
                Entries.Add(ToEntry(problem));
            }
        }
    }

    private HistoryEntryViewModel ToEntry(LockedLoadout loadout) => new(
        loadout.Timestamp.LocalDateTime.ToString("g"),
        loadout.Source switch
        {
            LockSource.Ready => "Confirmed (Ready)",
            LockSource.ScreenExit => "Screen closed",
            _ => "Manual recognition",
        } + (loadout.IsComplete ? string.Empty : " – incomplete"),
        loadout.Stratagems.Select(id => id is null
            ? StratagemTileViewModel.Empty
            : new StratagemTileViewModel(id, _catalog.Find(id)?.Name ?? id, _images.Get(id))).ToList(),
        loadout.MissionStratagems.Count == 0
            ? "–"
            : string.Join(", ", loadout.MissionStratagems.Select(id => _catalog.Find(id)?.Name ?? id)));

    private HistoryEntryViewModel ToEntry(RecognitionProblem problem) => new(
        problem.Timestamp.LocalDateTime.ToString("g"),
        problem.Kind switch
        {
            ProblemKind.UnrecognizedStratagem => "Problem: stratagem not recognized",
            ProblemKind.NoInputSequence => "Problem: input sequence unknown",
            ProblemKind.ScreenNotDetected => "Problem: loadout screen not detected",
            _ => "Problem: recognition error",
        },
        [],
        string.Empty,
        problem.Message,
        LoadImage(problem.ImagePath),
        problem.StratagemId is { } id ? new StratagemTileViewModel(id, _catalog.Find(id)?.Name ?? id, _images.Get(id)) : null);

    private static Bitmap? LoadImage(string? path)
    {
        try
        {
            return path is not null && File.Exists(path) ? new Bitmap(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
