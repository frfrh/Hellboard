// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HellboardCompanion.App.ViewModels;
using HellboardCompanion.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.App.Views;

public sealed partial class MainWindow : Window
{
    private SettingsStore? _settings;
    private ILogger? _logger;

    /// <summary>Position and size in the normal state (not maximized or minimized), stored when the window closes.</summary>
    private WindowPlacement? _normal;

    /// <summary>Restored position, set again once the window is open (in case the platform placed it elsewhere).</summary>
    private PixelPoint? _restoredPosition;

    private bool _placementStored;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Opens the window where it was closed last (if that is still on a screen) and stores its place when it closes.</summary>
    public void RememberPlacement(SettingsStore settings, ILogger? logger = null)
    {
        _settings = settings;
        _logger = logger;
        if (settings.Settings.Ui.MainWindow is { } placement)
        {
            var screens = Screens.All.Select(s => new Core.Platform.PixelRect(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height)).ToList();
            if (placement.IsReachable(screens))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(placement.X, placement.Y);
                Width = Math.Max(placement.Width, MinWidth);
                Height = Math.Max(placement.Height, MinHeight);
                WindowState = placement.IsMaximized ? WindowState.Maximized : WindowState.Normal;
                _normal = placement with { IsMaximized = false };
                _restoredPosition = Position;
                _logger?.LogInformation("Main window restored: {Placement}", placement);
            }
            else
            {
                _logger?.LogInformation("Main window placement {Placement} is not on any screen ({Screens}), opening centered", placement, string.Join("; ", screens));
            }
        }

        PositionChanged += (_, _) => NoteNormalPlacement();
        Opened += (_, _) =>
        {
            if (_restoredPosition is { } position && WindowState == WindowState.Normal && Position != position)
            {
                _logger?.LogInformation("Main window opened at {Actual} instead of {Restored}, moving it", Position, position);
                Position = position;
            }

            NoteNormalPlacement();
        };
        Closing += (_, _) => StorePlacement();
    }

    /// <summary>
    /// Stores position and size; called when the window closes and before the Companion exits (the exit does not
    /// close the window properly, so <see cref="Window.Closing"/> may not come, or only while the window is torn down).
    /// </summary>
    public void StorePlacement()
    {
        if (_settings is null || _placementStored)
        {
            return;
        }

        // The position noted while the window was moved, not the current one: while closing it may be wrong already.
        var normal = _normal ?? (WindowState == WindowState.Normal
            ? new WindowPlacement(Position.X, Position.Y, ClientSize.Width, ClientSize.Height, false)
            : null);
        if (normal is null)
        {
            return;
        }

        _placementStored = true;
        var placement = normal with { IsMaximized = WindowState == WindowState.Maximized };
        _settings.Settings.Ui.MainWindow = placement;
        try
        {
            _settings.SaveQuietly();
            _logger?.LogInformation("Main window placement stored: {Placement}", placement);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The window still closes; it opens centered next time.
            _logger?.LogWarning(ex, "Storing the main window placement failed");
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClientSizeProperty)
        {
            NoteNormalPlacement();
        }
    }

    private void NoteNormalPlacement()
    {
        if (_settings is null || _placementStored)
        {
            return;
        }

        // Moving and resizing arrive before the new window state when the window is maximized or minimized:
        // look at the state only after the current window message.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!_placementStored && WindowState == WindowState.Normal && IsVisible)
                {
                    _normal = new WindowPlacement(Position.X, Position.Y, ClientSize.Width, ClientSize.Height, false);
                }
            },
            DispatcherPriority.Background);
    }

    private async void OnAnalyzeImageFileClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Analyze image file",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"] }],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await viewModel.Manual.AnalyzeFileAsync(path);
        }
    }

    /// <summary>A large tile of the overview: open the picker at it (manual-selection plan §1.2).</summary>
    private void OnLoadoutTileClicked(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button { DataContext: LoadoutTileViewModel tile } button || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var picker = viewModel.Overview.CreatePicker(tile.Slot);
        var view = new StratagemPickerView { DataContext = picker };
        var flyout = new Flyout { Content = view, Placement = PlacementMode.BottomEdgeAlignedLeft, FlyoutPresenterClasses = { "picker" } };
        picker.Chosen += (_, _) => flyout.Hide();
        view.KeyDown += (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Escape)
            {
                flyout.Hide();
                args.Handled = true;
            }
        };
        flyout.ShowAt(button);
        e.Handled = true;
    }

    /// <summary>Right-click on a large tile: back to the recognized stratagem (manual-selection plan §1.3).</summary>
    private void OnLoadoutTileContextRequested(object? sender, Avalonia.Input.ContextRequestedEventArgs e)
    {
        if ((e.Source as Avalonia.StyledElement)?.DataContext is LoadoutTileViewModel { CanReset: true } tile && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Overview.ResetToRecognized(tile.Slot);
            e.Handled = true;
        }
    }

    private async void OnCopyVersionClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(viewModel.FullVersion);
        }
    }

    private void OnCodeInputPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => (sender as Control)?.Focus();

    /// <summary>Arrow keys or WASD enter the stratagem code (stratagem editor).</summary>
    private void OnCodeKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var editor = viewModel.StratagemEditor;
        var direction = e.Key switch
        {
            Avalonia.Input.Key.Up or Avalonia.Input.Key.W => "U",
            Avalonia.Input.Key.Down or Avalonia.Input.Key.S => "D",
            Avalonia.Input.Key.Left or Avalonia.Input.Key.A => "L",
            Avalonia.Input.Key.Right or Avalonia.Input.Key.D => "R",
            _ => null,
        };
        if (direction is not null)
        {
            editor.AddArrowCommand.Execute(direction);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.Back)
        {
            editor.RemoveArrowCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void OnExportStratagemsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export your stratagems",
            SuggestedFileName = "hellboard-stratagems.json",
            FileTypeChoices = [new FilePickerFileType("Stratagem list") { Patterns = ["*.json"] }],
        });
        if (file?.TryGetLocalPath() is { } path)
        {
            viewModel.StratagemEditor.ExportTo(path);
        }
    }

    private async void OnImportStratagemsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import stratagems",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Stratagem list") { Patterns = ["*.json"] }],
        });
        if (files.Count == 1 && files[0].TryGetLocalPath() is { } path)
        {
            viewModel.StratagemEditor.ImportFrom(path);
        }
    }

    private async void OnCopyHellboardLogClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(viewModel.HellboardTab.LogText());
        }
    }

    private async void OnOpenSnapshotFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var directory = Directory.CreateDirectory(viewModel.Capture.SnapshotDirectory);
        await Launcher.LaunchDirectoryInfoAsync(directory);
    }
}
