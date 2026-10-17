// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellboardCompanion.App.Services;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Configuration;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.App.ViewModels;

public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Settings tab (implementation plan §6.7, §6.8). Every change is saved and applied immediately.</summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore _store;
    private readonly CompanionSettings _settings;
    private readonly StratagemCatalog _catalog;
    private readonly StratagemImages _images;
    private readonly ManualTriggerService? _trigger;
    private readonly IAutostart? _autostart;
    private readonly StratagemIconStore? _iconStore;

    [ObservableProperty]
    private string _hotkeyText;

    [ObservableProperty]
    private string _hotkeyStatus = string.Empty;

    [ObservableProperty]
    private string _portText;

    [ObservableProperty]
    private string _processNameText;

    [ObservableProperty]
    private StratagemTileViewModel? _selectedFixedEntry;

    [ObservableProperty]
    private StratagemTileViewModel? _stratagemToAdd;

    public SettingsViewModel(
        SettingsStore store,
        StratagemCatalog catalog,
        StratagemImages images,
        ManualTriggerService? trigger,
        IAutostart? autostart,
        StratagemIconStore? iconStore = null)
    {
        _iconStore = iconStore;
        if (_iconStore is not null)
        {
            _iconStore.UpdateFinished += OnIconUpdateFinished;
        }

        _store = store;
        _settings = store.Settings;
        _catalog = catalog;
        _images = images;
        _trigger = trigger;
        _autostart = autostart;
        _hotkeyText = _settings.Recognition.TriggerHotkey;
        _portText = _settings.Hellboard.Port ?? string.Empty;
        _processNameText = _settings.Game.ProcessName;

        AvailableStratagems = catalog.Entries
            .Where(e => e.IsTransferable)
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new StratagemTileViewModel(e.Id, e.Name, null))
            .ToList();
        foreach (var id in _settings.MultiFunction.FixedList)
        {
            FixedList.Add(Tile(id));
        }

        if (_trigger is not null)
        {
            _trigger.HotkeyChanged += OnHotkeyChanged;
        }

        UpdateHotkeyStatus();
    }

    public IReadOnlyList<Choice<RecognitionMode>> RecognitionModes { get; } =
    [
        new(Core.Pipeline.RecognitionMode.AutomaticWithManualTrigger, "Automatic + hotkey"),
        new(Core.Pipeline.RecognitionMode.Automatic, "Automatic only"),
        new(Core.Pipeline.RecognitionMode.ManualOnly, "Hotkey only"),
    ];

    public IReadOnlyList<Choice<HellboardConnectionMode>> ConnectionModes { get; } =
    [
        new(HellboardConnectionMode.Serial, "Hellboard (USB)"),
        new(HellboardConnectionMode.Simulator, "Simulator (no board needed)"),
        new(HellboardConnectionMode.DryRun, "Dry run (send nothing)"),
    ];

    public IReadOnlyList<Choice<MultiFunctionMode>> MultiFunctionModes { get; } =
    [
        new(Core.Hellboard.MultiFunctionMode.FixedListAndAutoDetect, "Fixed list + automatic detection"),
        new(Core.Hellboard.MultiFunctionMode.FixedList, "Fixed list"),
        new(Core.Hellboard.MultiFunctionMode.AutoDetect, "Automatic detection"),
    ];

    public IReadOnlyList<StratagemTileViewModel> AvailableStratagems { get; }

    public ObservableCollection<StratagemTileViewModel> FixedList { get; } = [];

    public string ConfigFile => _store.FilePath;

    public Choice<RecognitionMode> RecognitionMode
    {
        get => RecognitionModes.First(c => c.Value == _settings.Recognition.Mode);
        set => Change(() => _settings.Recognition.Mode = value.Value);
    }

    public bool TestMode
    {
        get => !_settings.Recognition.ManualTransfer;
        set => Change(() => _settings.Recognition.ManualTransfer = !value);
    }

    public bool SaveSnapshotOnTrigger
    {
        get => _settings.Recognition.SaveSnapshotOnTrigger;
        set => Change(() => _settings.Recognition.SaveSnapshotOnTrigger = value);
    }

    public Choice<HellboardConnectionMode> ConnectionMode
    {
        get => ConnectionModes.First(c => c.Value == _settings.Hellboard.Mode);
        set => Change(() => _settings.Hellboard.Mode = value.Value);
    }

    public Choice<MultiFunctionMode> MultiFunctionMode
    {
        get => MultiFunctionModes.First(c => c.Value == _settings.MultiFunction.Mode);
        set => Change(() => _settings.MultiFunction.Mode = value.Value);
    }

    public bool StartWithWindows
    {
        get => _settings.General.StartWithWindows;
        set => Change(() =>
        {
            _settings.General.StartWithWindows = value;
            _autostart?.SetEnabled(value);
        });
    }

    public bool AutostartAvailable => _autostart is not null;

    // ---- stratagem icons (implementation plan §6.11) -----------------------------------------

    /// <summary>Number of icons, last check and its result.</summary>
    public string IconStatus => _iconStore is null
        ? "–"
        : $"{_iconStore.Icons.Count} icons"
          + (_iconStore.LastResult?.CheckedAt is { } at ? $", checked {at.LocalDateTime:g}" : string.Empty)
          + (_iconStore.LastError is { } error ? $" – last check failed: {error}"
             : _iconStore.LastResult is { Checked: true } result ? $" – {result.Summary}" : string.Empty);

    /// <summary>Icons in the repository whose stratagem has no input code yet (new stratagems).</summary>
    public string? IconsWithoutCode => _iconStore?.WithoutCode(_catalog) is { Count: > 0 } icons
        ? $"No input code known yet for: {string.Join(", ", icons.Select(i => i.Name))}. "
          + "They are recognized, but cannot be sent to the Hellboard until their code is added to "
          + "%APPDATA%\\HellboardCompanion\\stratagems.json (or a newer release)."
        : null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckIconsCommand))]
    private bool _isCheckingIcons;

    [RelayCommand(CanExecute = nameof(CanCheckIcons))]
    private async Task CheckIconsAsync()
    {
        if (_iconStore is null)
        {
            return;
        }

        IsCheckingIcons = true;
        try
        {
            await _iconStore.UpdateAsync(force: true);
        }
        finally
        {
            IsCheckingIcons = false;
        }
    }

    private bool CanCheckIcons() => _iconStore is not null && !IsCheckingIcons;

    private void OnIconUpdateFinished(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        OnPropertyChanged(nameof(IconStatus));
        OnPropertyChanged(nameof(IconsWithoutCode));
    });

    public void Dispose()
    {
        if (_iconStore is not null)
        {
            _iconStore.UpdateFinished -= OnIconUpdateFinished;
        }

        if (_trigger is not null)
        {
            _trigger.HotkeyChanged -= OnHotkeyChanged;
        }
    }

    [RelayCommand]
    private void ApplyHotkey()
    {
        try
        {
            var hotkey = Hotkey.Parse(HotkeyText);
            HotkeyText = hotkey.ToString();
            Change(() => _settings.Recognition.TriggerHotkey = hotkey.ToString());
        }
        catch (FormatException ex)
        {
            HotkeyStatus = ex.Message;
        }
    }

    [RelayCommand]
    private void ApplyPort() => Change(() => _settings.Hellboard.Port = string.IsNullOrWhiteSpace(PortText) ? null : PortText.Trim().ToUpperInvariant());

    [RelayCommand]
    private void ApplyProcessName()
    {
        if (!string.IsNullOrWhiteSpace(ProcessNameText))
        {
            Change(() => _settings.Game.ProcessName = ProcessNameText.Trim());
        }
    }

    [RelayCommand]
    private void AddStratagem()
    {
        if (StratagemToAdd?.Id is { } id && FixedList.All(t => t.Id != id))
        {
            FixedList.Add(Tile(id));
            SaveFixedList();
        }
    }

    [RelayCommand]
    private void RemoveStratagem(StratagemTileViewModel? tile)
    {
        if (tile is not null && FixedList.Remove(tile))
        {
            SaveFixedList();
        }
    }

    [RelayCommand]
    private void MoveUp(StratagemTileViewModel? tile) => Move(tile, -1);

    [RelayCommand]
    private void MoveDown(StratagemTileViewModel? tile) => Move(tile, +1);

    private void Move(StratagemTileViewModel? tile, int direction)
    {
        var index = tile is null ? -1 : FixedList.IndexOf(tile);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= FixedList.Count)
        {
            return;
        }

        FixedList.Move(index, target);
        SaveFixedList();
    }

    private void SaveFixedList() =>
        Change(() => _settings.MultiFunction.FixedList = FixedList.Select(t => t.Id!).ToList());   // new list: read by other threads

    private void Change(Action change)
    {
        change();
        _store.Save();
        OnPropertyChanged(string.Empty);
    }

    private StratagemTileViewModel Tile(string id) =>
        new(id, _catalog.Find(id)?.Name ?? id, _images.Get(id), _catalog.Find(id)?.IsTransferable == false ? "no input sequence yet" : null);

    private void OnHotkeyChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateHotkeyStatus);

    private void UpdateHotkeyStatus() =>
        HotkeyStatus = _trigger?.RegisteredHotkey is { } registered
            ? $"Active: {registered}"
            : _trigger?.HotkeyProblem ?? (_settings.Recognition.Mode == Core.Pipeline.RecognitionMode.Automatic ? "Disabled in automatic-only mode" : "Not registered");
}
