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
        services.AddSingleton(sp =>
        {
            var store = new StratagemReferenceStore(paths, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StratagemReferenceStore>>());
            store.Load();
            return store;
        });
        services.AddSingleton<LoadoutHistory>();
        services.AddSingleton(settings.Settings.Recognition);
        services.AddSingleton(sp =>
        {
            var store = new WikiStratagemStore(paths, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WikiStratagemStore>>());
            store.Load();
            return store;
        });
        services.AddSingleton(sp =>
        {
            // Built-in list < wiki layer (local copy of the wiki's stratagem table) < user list (wiki-source-plan §3.2).
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StratagemCatalog>>();
            var wiki = sp.GetRequiredService<WikiStratagemStore>();
            var catalog = StratagemCatalog.Load(Path.Combine(paths.ConfigDirectory, "stratagems.json"), logger, wiki.Stratagems);
            wiki.Changed += (_, _) => catalog.Reload(logger, wiki.Stratagems);
            return catalog;
        });
        services.AddSingleton(settings.Settings.Hellboard);
        services.AddSingleton(settings.Settings.MultiFunction);
        services.AddSingleton(settings.Settings.Board);
        services.AddSingleton(sp => new HellboardLink(
            sp.GetRequiredService<HellboardOptions>(),
            sp.GetRequiredService<MultiFunctionOptions>(),
            sp.GetRequiredService<StratagemCatalog>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HellboardLink>>(),
            sp.GetService<ISerialPortDiscovery>(),
            board: sp.GetRequiredService<BoardOptions>()));
        services.AddSingleton(sp => new LoadoutPipeline(
            sp.GetRequiredService<GameFrameGrabber>(),
            sp.GetRequiredService<StratagemIconStore>(),
            sp.GetRequiredService<LoadoutHistory>(),
            sp.GetRequiredService<RecognitionOptions>(),
            sp.GetRequiredService<IPlatformPaths>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LoadoutPipeline>>(),
            sp.GetRequiredService<StratagemCatalog>(),
            sp.GetRequiredService<StratagemReferenceStore>()));
        services.AddSingleton(sp => new ManualTriggerService(
            sp.GetRequiredService<LoadoutPipeline>(),
            sp.GetRequiredService<RecognitionOptions>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ManualTriggerService>>(),
            sp.GetService<IGlobalHotkeys>()));
        return services;
    }
}
