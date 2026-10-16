// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Configuration;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Loadout;
using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace HellboardCompanion.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform-neutral services. Platform-specific services are added by the
    /// platform project (e.g. <c>AddWindowsPlatform()</c>).
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IPlatformPaths paths)
    {
        var settings = SettingsStore.Load(paths);
        services.AddSingleton(paths);
        services.AddSingleton(settings);
        services.AddSingleton(settings.Settings.General);
        services.AddSingleton(settings.Settings.Game);
        services.AddSingleton<GameFrameGrabber>();
        services.AddSingleton<StratagemIconStore>();
        services.AddSingleton<LoadoutHistory>();
        services.AddSingleton(settings.Settings.Recognition);
        services.AddSingleton(StratagemCatalog.LoadSeed());
        services.AddSingleton(settings.Settings.Hellboard);
        services.AddSingleton(settings.Settings.MultiFunction);
        services.AddSingleton(sp => new HellboardLink(
            sp.GetRequiredService<HellboardOptions>(),
            sp.GetRequiredService<MultiFunctionOptions>(),
            sp.GetRequiredService<StratagemCatalog>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HellboardLink>>(),
            sp.GetService<ISerialPortDiscovery>()));
        services.AddSingleton(sp => new LoadoutPipeline(
            sp.GetRequiredService<GameFrameGrabber>(),
            sp.GetRequiredService<StratagemIconStore>(),
            sp.GetRequiredService<LoadoutHistory>(),
            sp.GetRequiredService<RecognitionOptions>(),
            sp.GetRequiredService<IPlatformPaths>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LoadoutPipeline>>(),
            sp.GetRequiredService<StratagemCatalog>()));
        services.AddSingleton(sp => new ManualTriggerService(
            sp.GetRequiredService<LoadoutPipeline>(),
            sp.GetRequiredService<RecognitionOptions>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ManualTriggerService>>(),
            sp.GetService<IGlobalHotkeys>()));
        return services;
    }
}
