// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellboardCompanion.Core.Configuration;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Hellboard.Update;

namespace HellboardCompanion.App.ViewModels;

public enum LogFilter
{
    All,
    InfoAndAbove,
    WarningsAndErrors,
}

/// <summary>One line of the firmware log.</summary>
public sealed record HellboardLogLineViewModel(string Time, string Level, string Message, IBrush LevelBrush);

/// <summary>
/// Hellboard tab (decisions F12, F13): connection, board configuration and the firmware log. The
/// configuration is saved in the Companion settings and sent to the board immediately (RAM); the
/// separate button writes it to the board's flash.
/// </summary>
public sealed partial class HellboardViewModel : ObservableObject, IDisposable
{
    private const int MaxLines = 500;

    private readonly SettingsStore? _store;
    private readonly BoardOptions _board;
    private readonly HellboardLink? _link;
    private readonly DispatcherTimer _saveTimer;
    private readonly List<HellboardLogEntry> _entries = [];
    private readonly Lazy<FirmwareBundle?> _bundle;
    private HellboardState? _state;

    [ObservableProperty]
    private string _configState = "–";

    [ObservableProperty]
    private string? _saveResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private string _boardFirmware = "–";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartUpdateCommand))]
    private bool _canUpdate;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isConfirmingUpdate;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartUpdateCommand))]
    private bool _isUpdating;

    [ObservableProperty]
    private string? _updateResult;

    public HellboardViewModel(SettingsStore? store, HellboardLink? link, StratagemCatalog catalog, HellboardStatusViewModel status)
    {
        _store = store;
        _board = store?.Settings.Board ?? new BoardOptions();
        _link = link;
        Status = status;
        _bundle = new Lazy<FirmwareBundle?>(() =>
        {
            try
            {
                return FirmwareBundle.LoadEmbedded(catalog);
            }
            catch (FormatException)
            {
                return null;
            }
        });
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _store?.Save();   // SettingsStore.Saved → HellboardLink.ApplySettings → setConfig
        };

        Stratagems =
        [
            new Choice<string?>(null, "– (button off)"),
            .. catalog.Entries
                .Where(e => e.IsTransferable)
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => new Choice<string?>(e.Id, e.Name)),
        ];

        if (_link is not null)
        {
            _link.StateChanged += OnStateChanged;
            _link.LogReceived += OnLogReceived;
            _link.BoardConfigAdopted += OnBoardConfigAdopted;
            _entries.AddRange(_link.Log);
            ApplyState(_link.State);
        }

        RebuildLog();
    }

    public HellboardStatusViewModel Status { get; }

    // ---- configuration -----------------------------------------------------------------------

    public IReadOnlyList<Choice<string?>> Stratagems { get; }

    public Choice<string?> FixedButton1
    {
        get => FindStratagem(_board.FixedButton1);
        set => Change(() => _board.FixedButton1 = value?.Value);
    }

    public Choice<string?> FixedButton2
    {
        get => FindStratagem(_board.FixedButton2);
        set => Change(() => _board.FixedButton2 = value?.Value);
    }

    public IReadOnlyList<Choice<int>> ModifierKeys => HidKeys.All;

    public Choice<int> ModifierKey
    {
        get => HidKeys.Find(_board.ModifierKey);
        set => Change(() => _board.ModifierKey = value?.Value ?? _board.ModifierKey);
    }

    public IReadOnlyList<Choice<bool>> ModifierModes { get; } =
    [
        new(false, "Hold the key while the code is entered"),
        new(true, "Press the key once to open the menu"),
    ];

    public Choice<bool> ModifierMode
    {
        get => ModifierModes.First(m => m.Value == _board.ModifierToggle);
        set => Change(() => _board.ModifierToggle = value?.Value ?? false);
    }

    public IReadOnlyList<Choice<DirectionKeys>> DirectionKeyChoices { get; } =
    [
        new(Core.Hellboard.DirectionKeys.Arrows, "Arrow keys"),
        new(Core.Hellboard.DirectionKeys.Wasd, "W A S D"),
    ];

    public Choice<DirectionKeys> DirectionKeys
    {
        get => DirectionKeyChoices.First(d => d.Value == _board.DirectionKeys);
        set => Change(() => _board.DirectionKeys = value?.Value ?? Core.Hellboard.DirectionKeys.Arrows);
    }

    public decimal MinKeyDelay => BoardConfiguration.MinKeyDelayMs;

    public decimal MaxKeyDelay => BoardConfiguration.MaxKeyDelayMs;

    public decimal? KeyDelayMs
    {
        get => _board.KeyDelayMs;
        set
        {
            if (value is { } delay)
            {
                Change(() => _board.KeyDelayMs = (int)Math.Clamp(delay, MinKeyDelay, MaxKeyDelay));
            }
        }
    }

    public double Brightness
    {
        get => _board.Brightness;
        set
        {
            Change(() => _board.Brightness = (int)Math.Round(value));
            OnPropertyChanged(nameof(BrightnessText));
        }
    }

    public string BrightnessText => $"{Math.Round(_board.Brightness * 100 / 255.0)} %";

    public IReadOnlyList<Choice<BoardLogLevel>> LogLevels { get; } =
    [
        new(BoardLogLevel.Debug, "Debug (everything, incl. every key press)"),
        new(BoardLogLevel.Info, "Info"),
        new(BoardLogLevel.Warn, "Warnings and errors"),
        new(BoardLogLevel.Error, "Errors only"),
    ];

    public Choice<BoardLogLevel> LogLevel
    {
        get => LogLevels.First(l => l.Value == _board.LogLevel);
        set => Change(() => _board.LogLevel = value?.Value ?? BoardLogLevel.Info);
    }

    /// <summary>"Save configuration on the Hellboard" (F13): the only way the configuration reaches the board's flash.</summary>
    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task SaveConfigAsync()
    {
        if (_link is null)
        {
            return;
        }

        _saveTimer.Stop();
        _store?.Save();
        SaveResult = "Saving …";
        try
        {
            await _link.SaveBoardConfigAsync();
            SaveResult = $"{DateTime.Now:HH:mm:ss} – configuration saved on the Hellboard";
        }
        catch (Exception ex)
        {
            SaveResult = $"Not saved: {ex.Message}";
        }
    }

    // ---- firmware update (implementation plan §6.10) --------------------------------------------

    public string BundledFirmware => _bundle.Value?.Version ?? "not available";

    /// <summary>What the update installs, shown before it starts.</summary>
    public string UpdateDetails => _bundle.Value is { } bundle
        ? $"Installs firmware {bundle.Version} ({bundle.Files.Count} files: {string.Join(", ", bundle.Files.Select(f => f.Target))}). "
          + "The configuration saved on the Hellboard is kept. Every file is checked on the board before the old files are replaced; if anything fails, the old firmware stays. "
          + "Do not unplug the Hellboard during the update (about one minute)."
        : string.Empty;

    public ObservableCollection<string> UpdateSteps { get; } = [];

    [RelayCommand(CanExecute = nameof(CanStartUpdate))]
    private void RequestUpdate()
    {
        UpdateResult = null;
        IsConfirmingUpdate = true;
    }

    [RelayCommand]
    private void CancelUpdate() => IsConfirmingUpdate = false;

    [RelayCommand(CanExecute = nameof(CanStartUpdate))]
    private async Task StartUpdateAsync()
    {
        if (_link is null || _bundle.Value is not { } bundle)
        {
            return;
        }

        IsConfirmingUpdate = false;
        IsUpdating = true;
        UpdateSteps.Clear();
        UpdateResult = "Updating …";
        var progress = new Progress<string>(step => UpdateSteps.Add($"{DateTime.Now:HH:mm:ss}  {step}"));
        try
        {
            await _link.UpdateFirmwareAsync(bundle, progress);
            UpdateResult = $"Firmware {bundle.Version} installed – the Hellboard restarts and connects again.";
        }
        catch (Exception ex)
        {
            UpdateResult = $"Update failed: {ex.Message} The previous firmware is still on the Hellboard.";
        }
        finally
        {
            IsUpdating = false;
            UpdateCanUpdate();
        }
    }

    private bool CanStartUpdate() => CanUpdate && !IsUpdating;

    private void UpdateCanUpdate()
    {
        var state = _state;
        CanUpdate = _bundle.Value is not null && state is { Status: HellboardStatus.Connected or HellboardStatus.NoProtocol, Port: not null } && !IsUpdating;
        BoardFirmware = state switch
        {
            { Status: HellboardStatus.Connected, Info: { } info } => info.Firmware ?? "unknown",
            { Status: HellboardStatus.NoProtocol } => "older firmware without Companion support",
            { Status: HellboardStatus.Updating } => "updating …",
            _ => "–",
        };
        IsUpdateAvailable = CanUpdate && (state?.Status == HellboardStatus.NoProtocol || state?.Info?.Firmware != _bundle.Value?.Version);
    }

    // ---- log ---------------------------------------------------------------------------------

    public ObservableCollection<HellboardLogLineViewModel> LogLines { get; } = [];

    public IReadOnlyList<Choice<LogFilter>> LogFilters { get; } =
    [
        new(ViewModels.LogFilter.All, "All"),
        new(ViewModels.LogFilter.InfoAndAbove, "Info and above"),
        new(ViewModels.LogFilter.WarningsAndErrors, "Warnings and errors"),
    ];

    [ObservableProperty]
    private Choice<LogFilter>? _logFilter;

    public bool HasLog => LogLines.Count > 0;

    /// <summary>Visible log lines as plain text (for the clipboard), oldest first.</summary>
    public string LogText() => string.Join(Environment.NewLine, LogLines.Reverse().Select(l => $"{l.Time} {l.Level,-5} {l.Message}"));

    [RelayCommand]
    private void ClearLog()
    {
        _entries.Clear();
        RebuildLog();
    }

    partial void OnLogFilterChanged(Choice<LogFilter>? value) => RebuildLog();

    public void Dispose()
    {
        _saveTimer.Stop();
        if (_link is not null)
        {
            _link.StateChanged -= OnStateChanged;
            _link.LogReceived -= OnLogReceived;
            _link.BoardConfigAdopted -= OnBoardConfigAdopted;
        }
    }

    private void Change(Action apply)
    {
        apply();
        _saveTimer.Stop();
        _saveTimer.Start();   // sliders change often; save and send once they settle
    }

    private Choice<string?> FindStratagem(string? id) =>
        Stratagems.FirstOrDefault(s => s.Value == id) ?? new Choice<string?>(id, id ?? "–");

    private void OnStateChanged(object? sender, HellboardState state) => Dispatcher.UIThread.Post(() => ApplyState(state));

    private void ApplyState(HellboardState state)
    {
        _state = state;
        UpdateCanUpdate();
        IsConnected = state.Status == HellboardStatus.Connected;
        ConfigState = !IsConnected
            ? "Not connected – the configuration is sent as soon as the Hellboard is connected."
            : state.ConfigSaved switch
            {
                true => "The Hellboard uses this configuration and has it saved.",
                false => "The Hellboard uses this configuration, but has not saved it yet – after a restart it uses its saved configuration until the Companion sends this one again.",
                _ => "Sending the configuration …",
            };
    }

    private void OnBoardConfigAdopted(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        OnPropertyChanged(string.Empty);   // all configuration properties
    });

    private void OnLogReceived(object? sender, HellboardLogEntry entry) => Dispatcher.UIThread.Post(() =>
    {
        _entries.Add(entry);
        if (_entries.Count > MaxLines)
        {
            _entries.RemoveAt(0);
        }

        if (Matches(entry))
        {
            LogLines.Insert(0, ToLine(entry));
            if (LogLines.Count > MaxLines)
            {
                LogLines.RemoveAt(LogLines.Count - 1);
            }

            OnPropertyChanged(nameof(HasLog));
        }
    });

    private void RebuildLog()
    {
        LogLines.Clear();
        foreach (var entry in Enumerable.Reverse(_entries).Where(Matches))
        {
            LogLines.Add(ToLine(entry));
        }

        OnPropertyChanged(nameof(HasLog));
    }

    private bool Matches(HellboardLogEntry entry) => (LogFilter?.Value ?? ViewModels.LogFilter.All) switch
    {
        ViewModels.LogFilter.InfoAndAbove => entry.Level != "debug",
        ViewModels.LogFilter.WarningsAndErrors => entry.Level is "warn" or "error",
        _ => true,
    };

    private static HellboardLogLineViewModel ToLine(HellboardLogEntry entry) => new(
        entry.Received.LocalDateTime.ToString("HH:mm:ss"),
        entry.Level,
        entry.Message,
        entry.Level switch
        {
            "error" => Brushes.IndianRed,
            "warn" => Brushes.Goldenrod,
            "debug" => Brushes.Gray,
            _ => Brushes.SteelBlue,
        });
}
