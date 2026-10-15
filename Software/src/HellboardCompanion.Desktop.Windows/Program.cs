// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using HellboardCompanion.App;
using HellboardCompanion.Core;
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

        try
        {
            return BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
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

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new CompanionApp(services))
            .UsePlatformDetect()
            .LogToTrace();
}
