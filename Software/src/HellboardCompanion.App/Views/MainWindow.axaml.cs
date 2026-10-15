// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
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
