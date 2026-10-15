// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using HellboardCompanion.Core;
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

    public MainWindowViewModel(IPlatformInfo platform, IPlatformPaths paths, IGameLocator? gameLocator)
    {
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

    public string Title => $"{AppInfo.ProductName} {AppInfo.Version}";

    public string OperatingSystem { get; }

    public string? SupportNote { get; }

    public bool HasSupportNote => SupportNote is not null;

    public string ConfigDirectory { get; }

    public string LogDirectory { get; }

    /// <summary>Instance with placeholder values for the XAML previewer.</summary>
    public static MainWindowViewModel DesignInstance =>
        new(new DesignPlatformInfo(), new PlatformPaths(@"C:\Users\Design\AppData\Roaming", @"C:\Users\Design\AppData\Local"), null);

    public void Dispose()
    {
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
