// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Threading;
using HellboardCompanion.App;
using HellboardCompanion.Core;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Configuration;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Platform;
using HellboardCompanion.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace HellboardCompanion.Desktop.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            return 0;   // already running; that instance shows its window
        }

        var paths = PlatformPaths.FromEnvironment();
        Directory.CreateDirectory(paths.LogDirectory);

        var serilog = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(paths.LogDirectory, "hellboard-companion-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        using var services = new ServiceCollection()
            .AddLogging(builder => builder.AddSerilog(serilog, dispose: true))
            .AddCoreServices(paths)
            .AddWindowsPlatform()
            .BuildServiceProvider();

        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("HellboardCompanion");
        log.LogInformation("{Product} {Version} starting", AppInfo.ProductName, AppInfo.Version);
        services.GetRequiredService<IGameLocator>().Start();
        var iconStore = services.GetRequiredService<StratagemIconStore>();
        var wikiStore = services.GetRequiredService<WikiStratagemStore>();
        _ = iconStore.UpdateAsync();
        _ = wikiStore.UpdateAsync();
        // Long-running sessions: look again every 6 h; the stores themselves check at most once a day.
        using var iconTimer = new Timer(_ =>
        {
            _ = iconStore.UpdateAsync();
            _ = wikiStore.UpdateAsync();
        }, null, TimeSpan.FromHours(6), TimeSpan.FromHours(6));
        var pipeline = services.GetRequiredService<LoadoutPipeline>();
        var hellboard = services.GetRequiredService<HellboardLink>();
        pipeline.LoadoutLocked += (_, loadout) => hellboard.Submit(loadout);
        services.GetRequiredService<StratagemCatalog>().Changed += (_, _) => hellboard.Rebuild();   // stratagem editor (§6.12), wiki layer
        var settings = services.GetRequiredService<SettingsStore>();
        hellboard.BoardConfigAdopted += (_, _) => settings.Save();   // first connect: keep the board's configuration (F13)
        hellboard.Start();
        pipeline.Start();
        var trigger = services.GetRequiredService<ManualTriggerService>();
        trigger.Start();

        if (settings.LoadProblem is { } problem)
        {
            log.LogWarning("{Problem}", problem);
        }

        if (settings.Settings.General.StartWithWindows)
        {
            services.GetRequiredService<IAutostart>().SetEnabled(true);   // keeps the path current after the EXE moved
        }

        settings.Saved += (_, _) =>
        {
            hellboard.ApplySettings();
            trigger.ApplySettings();
        };

        CompanionApp? app = null;
        instance.OnShowRequested(() => Dispatcher.UIThread.Post(() => app?.ShowMainWindow()));

        try
        {
            return AppBuilder.Configure(() => app = new CompanionApp(services))
                .UsePlatformDetect()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            log.LogCritical(ex, "Unhandled exception, shutting down");
            throw;
        }
        finally
        {
            log.LogInformation("Stopped");
        }
    }

    /// <summary>Entry point for the Avalonia XAML previewer (Rider / Visual Studio).</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure(() => new CompanionApp())
            .UsePlatformDetect()
            .LogToTrace();
}
