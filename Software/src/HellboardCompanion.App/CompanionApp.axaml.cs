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
using HellboardCompanion.Core;
using HellboardCompanion.Core.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.App;

/// <summary>
/// Tray application. The process keeps running when the window is closed and ends only via
/// the tray menu ("Exit").
/// </summary>
public sealed partial class CompanionApp : Application
{
    private const string RecognizeNowHeader = "Recognize loadout now";   // tray menu entry in CompanionApp.axaml

    private readonly IServiceProvider? _services;
    private MainWindow? _mainWindow;
    private bool _openingMainWindow;
    private bool _exiting;
    private StratagemImages? _images;

    /// <summary>Newer release found while only the tray icon was shown; offered when the window opens.</summary>
    private CompanionRelease? _pendingUpdate;

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

        if (_services?.GetService<IGameLocator>() is { } game)
        {
            game.Changed += (_, snapshot) => Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateRecognizeMenuItem(snapshot));
            UpdateRecognizeMenuItem(game.Current);
        }

        base.OnFrameworkInitializationCompleted();

        if (_services?.GetService<GeneralOptions>() is { StartMinimized: false })
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(ShowMainWindow);   // default: open the window; otherwise only the tray icon
        }

        if (_services?.GetService<GeneralOptions>() is { CheckForUpdates: true } general)
        {
            _ = CheckForUpdateAsync(general);
        }
    }

    /// <summary>
    /// Looks for a newer release a few seconds after the start (decided 2026-10-19). With the window open the
    /// user is asked right away; with only the tray icon Windows shows a notification and the question follows
    /// when the window is opened. Offline or on a GitHub error nothing happens (logged only).
    /// </summary>
    private async Task CheckForUpdateAsync(GeneralOptions general)
    {
        var logger = _services?.GetService<ILogger<CompanionApp>>();
        CompanionRelease? release;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            if (CompanionUpdateChecker.IsVersionOverridden)
            {
                logger?.LogWarning("Update check as version {Version} ({Variable} is set)", CompanionUpdateChecker.CurrentVersion.ToString(3),
                    CompanionUpdateChecker.VersionOverrideVariable);
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            release = await new CompanionUpdateChecker(http).FindUpdateAsync(general.SkippedVersion);
            logger?.LogInformation("Update check: {Result}", release is null
                ? $"{CompanionUpdateChecker.CurrentVersion.ToString(3)} is up to date" : $"{release.Tag} available");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            logger?.LogInformation("Update check not possible: {Error}", ex.Message);
            return;
        }

        if (release is null)
        {
            return;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_mainWindow is not null)
            {
                _ = OfferUpdateAsync(release);
                return;
            }

            _pendingUpdate = release;
            const string title = "New Hellboard Companion version";
            var body = $"Version {release.VersionText} is available. Open the Companion to download it.";
            if (_services?.GetService<INotifications>()?.Show(title, body) != true)
            {
                ToastWindow.Show(title, body);
            }
        });
    }

    /// <summary>Asks whether to download <paramref name="release"/>: opens its page in the browser, or remembers a skipped version.</summary>
    private async Task OfferUpdateAsync(CompanionRelease release)
    {
        if (_mainWindow is not { } owner)
        {
            return;
        }

        _pendingUpdate = null;
        switch (await new UpdateWindow(release).ShowDialog<UpdateChoice?>(owner))
        {
            case UpdateChoice.Download:
                await owner.Launcher.LaunchUriAsync(new Uri(release.Url));
                break;
            case UpdateChoice.Skip when _services?.GetService<SettingsStore>() is { } settings:
                settings.Settings.General.SkippedVersion = release.VersionText;
                settings.Save();
                break;
        }
    }

    /// <summary>"Recognize loadout now" in the tray menu only while the game window can be captured.</summary>
    private void UpdateRecognizeMenuItem(GameSnapshot snapshot)
    {
        var item = TrayIcon.GetIcons(this)?.SelectMany(icon => icon.Menu?.Items ?? []).OfType<NativeMenuItem>()
            .FirstOrDefault(i => i.Header == RecognizeNowHeader);
        if (item is not null)
        {
            item.IsEnabled = snapshot is { Status: GameStatus.Running, Location.IsMinimized: false };
        }
    }

    private void OnTrayIconClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnOpenClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnRecognizeNowClicked(object? sender, EventArgs e) => _ = _services?.GetService<ManualTriggerService>()?.TriggerAsync();

    private void OnExitClicked(object? sender, EventArgs e) => Exit();

    private void Exit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
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
                if (_services?.GetService<GeneralOptions>() is { CloseAction: CloseAction.Exit })
                {
                    Exit();   // setting "Close button (X)": exit instead of keeping the tray icon
                }
            };
            _mainWindow = window;
            window.Show();
            window.Activate();
            if (_pendingUpdate is { } release)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = OfferUpdateAsync(release));   // found while only the tray icon was shown
            }
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
            new SettingsViewModel(_services.GetRequiredService<SettingsStore>(), catalog, _images, _services.GetService<ManualTriggerService>(), _services.GetService<IAutostart>(), _services.GetService<StratagemIconStore>(),
                _services.GetService<WikiStratagemStore>()),
            new HistoryViewModel(_services.GetService<LoadoutHistory>(), _services.GetService<LoadoutPipeline>(), catalog, _images, _services.GetRequiredService<SettingsStore>(),
                _services.GetService<StratagemReferenceStore>()),
            new HellboardViewModel(_services.GetRequiredService<SettingsStore>(), _services.GetService<HellboardLink>(), catalog, hellboard),
            new StratagemEditorViewModel(catalog, _services.GetService<StratagemIconStore>(), _images, _services.GetService<Microsoft.Extensions.Logging.ILogger<StratagemEditorViewModel>>(),
                _services.GetService<StratagemReferenceStore>(), _services.GetService<WikiStratagemStore>()));
    }
}
