// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Interactivity;
using HellboardCompanion.Core;
using HellboardCompanion.Core.Updates;

namespace HellboardCompanion.App.Views;

/// <summary>What the user chose in the <see cref="UpdateWindow"/>.</summary>
public enum UpdateChoice
{
    Later,
    Download,
    Skip,
}

public sealed partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();
    }

    public UpdateWindow(CompanionRelease release)
        : this()
    {
        TitleText.Text = $"Hellboard Companion {release.VersionText} is available";
        BodyText.Text = $"You are using version {CompanionUpdateChecker.CurrentVersionText}. Download opens the release page in your browser, "
            + "where you find the installer and the ZIP package.";
    }

    private void OnDownloadClicked(object? sender, RoutedEventArgs e) => Close(UpdateChoice.Download);

    private void OnLaterClicked(object? sender, RoutedEventArgs e) => Close(UpdateChoice.Later);

    private void OnSkipClicked(object? sender, RoutedEventArgs e) => Close(UpdateChoice.Skip);
}
