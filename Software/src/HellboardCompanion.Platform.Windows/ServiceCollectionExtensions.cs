// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace HellboardCompanion.Platform.Windows;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Windows implementations of the platform interfaces from
    /// <c>HellboardCompanion.Core.Platform</c>.
    /// </summary>
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformInfo, WindowsPlatformInfo>();
        return services;
    }
}
