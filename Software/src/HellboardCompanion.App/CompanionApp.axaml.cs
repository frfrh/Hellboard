// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HellboardCompanion.App.Services;
using HellboardCompanion.App.ViewModels;
using HellboardCompanion.App.Views;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Configuration;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Pipeline;
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
    private bool _openingMainWindow;
    private StratagemImages? _images;

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

        if (_services?.GetService<ManualTriggerService>() is { } trigger)
        {
            trigger.Completed += (_, result) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var (title, body) = ManualRecognitionText.Describe(result);
                ToastWindow.Show(title, body);
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayIconClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnOpenClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnRecognizeNowClicked(object? sender, EventArgs e) => _ = _services?.GetService<ManualTriggerService>()?.TriggerAsync();

    private void OnExitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>Opens the status window, or brings it to the front if it is already open.</summary>
    public void ShowMainWindow()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Activate();
            return;
        }

        // Creating the window for the first time takes a moment, and Avalonia processes window
        // messages meanwhile: the second click of a double-click on the tray icon arrives here.
        if (_openingMainWindow)
        {
            return;
        }

        _openingMainWindow = true;
        try
        {
            var viewModel = CreateMainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            window.Closed += (_, _) =>
            {
                viewModel.Dispose();
                _mainWindow = null;
            };
            _mainWindow = window;
            window.Show();
            window.Activate();
        }
        finally
        {
            _openingMainWindow = false;
        }
    }

    private MainWindowViewModel CreateMainWindowViewModel()
    {
        if (_services is null)
        {
            return MainWindowViewModel.DesignInstance;
        }

        _images ??= new StratagemImages(_services.GetService<StratagemIconStore>(), _services.GetRequiredService<StratagemCatalog>(),
            _services.GetService<StratagemReferenceStore>());
        var catalog = _services.GetRequiredService<StratagemCatalog>();
        var hellboard = new HellboardStatusViewModel(_services.GetService<HellboardLink>());
        return new MainWindowViewModel(
            _services.GetRequiredService<IPlatformInfo>(),
            _services.GetRequiredService<IPlatformPaths>(),
            _services.GetService<IGameLocator>(),
            new CapturePreviewViewModel(_services.GetService<GameFrameGrabber>(), _services.GetRequiredService<IPlatformPaths>()),
            new LoadoutStatusViewModel(_services.GetService<LoadoutPipeline>(), _services.GetService<StratagemIconStore>()),
            new ManualRecognitionViewModel(_services.GetService<ManualTriggerService>(), _services.GetService<LoadoutPipeline>(), _services.GetService<RecognitionOptions>()),
            hellboard,
            new OverviewViewModel(
                _services.GetService<IGameLocator>(), _services.GetService<LoadoutPipeline>(), _services.GetService<HellboardLink>(),
                catalog, _images, _services.GetService<ManualTriggerService>(), hellboard, _services.GetService<LoadoutHistory>(), _services.GetService<MultiFunctionOptions>()),
            new SettingsViewModel(_services.GetRequiredService<SettingsStore>(), catalog, _images, _services.GetService<ManualTriggerService>(), _services.GetService<IAutostart>(), _services.GetService<StratagemIconStore>()),
            new HistoryViewModel(_services.GetService<LoadoutHistory>(), _services.GetService<LoadoutPipeline>(), catalog, _images, _services.GetRequiredService<SettingsStore>(),
                _services.GetService<StratagemReferenceStore>()),
            new HellboardViewModel(_services.GetRequiredService<SettingsStore>(), _services.GetService<HellboardLink>(), catalog, hellboard),
            new StratagemEditorViewModel(catalog, _services.GetService<StratagemIconStore>(), _images, _services.GetService<Microsoft.Extensions.Logging.ILogger<StratagemEditorViewModel>>(),
                _services.GetService<StratagemReferenceStore>()));
    }
}
