// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using HellboardCompanion.App.Services;
using HellboardCompanion.Core;
using HellboardCompanion.Core.Configuration;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IGameLocator? _gameLocator;

    [ObservableProperty]
    private string _gameStatus = string.Empty;

    [ObservableProperty]
    private string _gameMonitor = "–";

    [ObservableProperty]
    private string _gameClientArea = "–";

    [ObservableProperty]
    private string _gameWindowMode = "–";

    public MainWindowViewModel(IPlatformInfo platform, IPlatformPaths paths, IGameLocator? gameLocator, CapturePreviewViewModel capture, LoadoutStatusViewModel loadout, ManualRecognitionViewModel manual, HellboardStatusViewModel hellboard, OverviewViewModel overview, SettingsViewModel settings, HistoryViewModel history)
    {
        Overview = overview;
        Settings = settings;
        History = history;
        Hellboard = hellboard;
        Capture = capture;
        Loadout = loadout;
        Manual = manual;
        OperatingSystem = platform.OperatingSystem;
        SupportNote = platform.SupportNote;
        ConfigDirectory = paths.ConfigDirectory;
        LogDirectory = paths.LogDirectory;

        _gameLocator = gameLocator;
        if (_gameLocator is not null)
        {
            _gameLocator.Changed += OnGameChanged;
            Apply(_gameLocator.Current);
        }
        else
        {
            Apply(GameSnapshot.NotRunning);
        }
    }

    public string Title => $"{AppInfo.ProductName} {AppInfo.ShortVersion}";

    /// <summary>Full version with git commit, shown and copyable in the diagnostics tab.</summary>
    public string FullVersion => AppInfo.Version;

    public string OperatingSystem { get; }

    public string? SupportNote { get; }

    public bool HasSupportNote => SupportNote is not null;

    public string ConfigDirectory { get; }

    public string LogDirectory { get; }

    public CapturePreviewViewModel Capture { get; }

    public LoadoutStatusViewModel Loadout { get; }

    public ManualRecognitionViewModel Manual { get; }

    public HellboardStatusViewModel Hellboard { get; }

    public OverviewViewModel Overview { get; }

    public SettingsViewModel Settings { get; }

    public HistoryViewModel History { get; }

    /// <summary>Instance with placeholder values for the XAML previewer.</summary>
    public static MainWindowViewModel DesignInstance
    {
        get
        {
            var catalog = StratagemCatalog.LoadSeed();
            var images = new StratagemImages(null);
            var hellboard = new HellboardStatusViewModel(null);
            var designSettings = SettingsStore.Load(new PlatformPaths(Path.Combine(Path.GetTempPath(), "HellboardCompanionDesign"), Path.Combine(Path.GetTempPath(), "HellboardCompanionDesign")));
            return new(
                new DesignPlatformInfo(), DesignPaths, null, new CapturePreviewViewModel(null, DesignPaths), new LoadoutStatusViewModel(null, null),
                new ManualRecognitionViewModel(null, null, null), hellboard,
                new OverviewViewModel(null, null, null, catalog, images, null, hellboard),
                new SettingsViewModel(designSettings, catalog, images, null, null),
                new HistoryViewModel(null, null, catalog, images));
        }
    }

    private static PlatformPaths DesignPaths => new(@"C:\Users\Design\AppData\Roaming", @"C:\Users\Design\AppData\Local");

    public void Dispose()
    {
        Capture.Dispose();
        Loadout.Dispose();
        Manual.Dispose();
        Hellboard.Dispose();
        Overview.Dispose();
        Settings.Dispose();
        History.Dispose();
        if (_gameLocator is not null)
        {
            _gameLocator.Changed -= OnGameChanged;
        }
    }

    private void OnGameChanged(object? sender, GameSnapshot snapshot) =>
        Dispatcher.UIThread.Post(() => Apply(snapshot));

    private void Apply(GameSnapshot snapshot)
    {
        var location = snapshot.Location;
        GameStatus = snapshot.Status switch
        {
            Core.Platform.GameStatus.NotRunning => "Not running",
            Core.Platform.GameStatus.Starting => "Starting…",
            _ when location!.IsMinimized => "Running (minimized)",
            _ when location!.IsForeground => "Running (in foreground)",
            _ => "Running (in background)",
        };
        GameMonitor = location?.Monitor.ToString() ?? "–";
        GameClientArea = location is null || location.IsMinimized ? "–" : location.ClientBounds.ToString();
        GameWindowMode = location is null || location.IsMinimized
            ? "–"
            : location.Mode == WindowMode.FullscreenOrBorderless ? "Fullscreen / borderless" : "Windowed";
    }

    private sealed class DesignPlatformInfo : IPlatformInfo
    {
        public string OperatingSystem => "Windows 11 (build 26200)";

        public PlatformSupportLevel SupportLevel => PlatformSupportLevel.Full;

        public string? SupportNote => null;
    }
}
