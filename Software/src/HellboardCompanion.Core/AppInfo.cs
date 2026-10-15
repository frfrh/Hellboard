// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;

namespace HellboardCompanion.Core;

/// <summary>
/// Static product information shared by all parts of the application.
/// </summary>
public static class AppInfo
{
    public const string ProductName = "Hellboard Companion";

    /// <summary>Name of the per-user directories (configuration, data, logs).</summary>
    public const string DirectoryName = "HellboardCompanion";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}
