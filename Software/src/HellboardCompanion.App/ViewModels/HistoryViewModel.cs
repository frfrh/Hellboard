// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using Avalonia.Threading;
using HellboardCompanion.App.Services;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Pipeline;

namespace HellboardCompanion.App.ViewModels;

/// <summary>One locked loadout in the history list.</summary>
public sealed record HistoryEntryViewModel(string Time, string Source, IReadOnlyList<StratagemTileViewModel> Stratagems, string MissionStratagems);

/// <summary>History tab: the most recent locked loadouts (implementation plan §6.7).</summary>
public sealed class HistoryViewModel : IDisposable
{
    private readonly LoadoutHistory? _history;
    private readonly LoadoutPipeline? _pipeline;
    private readonly StratagemCatalog _catalog;
    private readonly StratagemImages _images;

    public HistoryViewModel(LoadoutHistory? history, LoadoutPipeline? pipeline, StratagemCatalog catalog, StratagemImages images)
    {
        _history = history;
        _pipeline = pipeline;
        _catalog = catalog;
        _images = images;
        if (_pipeline is not null)
        {
            _pipeline.LoadoutLocked += OnLocked;
        }

        Reload();
    }

    public ObservableCollection<HistoryEntryViewModel> Entries { get; } = [];

    public string? FilePath => _history?.FilePath;

    public void Dispose()
    {
        if (_pipeline is not null)
        {
            _pipeline.LoadoutLocked -= OnLocked;
        }
    }

    private void OnLocked(object? sender, LockedLoadout loadout) => Dispatcher.UIThread.Post(Reload);

    private void Reload()
    {
        Entries.Clear();
        foreach (var entry in _history?.ReadRecent() ?? [])
        {
            Entries.Add(new HistoryEntryViewModel(
                entry.Timestamp.LocalDateTime.ToString("g"),
                entry.Source switch
                {
                    LockSource.Ready => "Ready",
                    LockSource.ScreenExit => "Screen closed",
                    _ => "Manual",
                } + (entry.IsComplete ? string.Empty : " – incomplete"),
                entry.Stratagems.Select(id => id is null
                    ? StratagemTileViewModel.Empty
                    : new StratagemTileViewModel(id, _catalog.Find(id)?.Name ?? id, _images.Get(id))).ToList(),
                entry.MissionStratagems.Count == 0
                    ? "–"
                    : string.Join(", ", entry.MissionStratagems.Select(id => _catalog.Find(id)?.Name ?? id))));
        }
    }
}
