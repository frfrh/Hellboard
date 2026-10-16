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

    private async void OnCopyVersionClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(viewModel.FullVersion);
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
