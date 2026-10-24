// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HellboardCompanion.App.ViewModels;

namespace HellboardCompanion.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
