// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HellboardCompanion.App.ViewModels;
using HellboardCompanion.App.Views;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace HellboardCompanion.App;

/// <summary>
/// Tray application. The process keeps running when the window is closed and ends only via
/// the tray menu ("Exit").
/// </summary>
public sealed partial class CompanionApp : Application
{
    private readonly IServiceProvider? _services;
    private MainWindow? _mainWindow;

    /// <summary>Used by the XAML previewer only.</summary>
    public CompanionApp()
    {
    }

    public CompanionApp(IServiceProvider services)
    {
        _services = services;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayIconClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnOpenClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnExitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow { DataContext = CreateMainWindowViewModel() };
            _mainWindow.Closed += (_, _) => _mainWindow = null;
            _mainWindow.Show();
        }

        _mainWindow.Activate();
    }

    private MainWindowViewModel CreateMainWindowViewModel() => _services is null
        ? MainWindowViewModel.DesignInstance
        : new MainWindowViewModel(
            _services.GetRequiredService<IPlatformInfo>(),
            _services.GetRequiredService<IPlatformPaths>());
}
