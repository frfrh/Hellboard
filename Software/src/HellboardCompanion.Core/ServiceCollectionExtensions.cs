// SPDX-License-Identifier: GPL-3.0-or-later

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
        services.AddSingleton(paths);
        return services;
    }
}
