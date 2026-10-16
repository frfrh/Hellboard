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

    /// <summary>Full version including the git commit, e.g. "0.1.0+b6d8337…" (diagnostics, logs).</summary>
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>Version without build metadata, e.g. "0.1.0" (window title).</summary>
    public static string ShortVersion { get; } = Version.Split('+')[0];
}
