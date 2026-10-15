// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>
/// Default <see cref="IPlatformPaths"/> based on the .NET special folders, which map to the
/// correct per-user locations on Windows and Linux.
/// </summary>
public sealed class PlatformPaths : IPlatformPaths
{
    public PlatformPaths(string roamingAppDataRoot, string localAppDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roamingAppDataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppDataRoot);

        ConfigDirectory = Path.Combine(roamingAppDataRoot, AppInfo.DirectoryName);
        DataDirectory = Path.Combine(localAppDataRoot, AppInfo.DirectoryName);
        LogDirectory = Path.Combine(DataDirectory, "logs");
    }

    public string ConfigDirectory { get; }

    public string DataDirectory { get; }

    public string LogDirectory { get; }

    public static PlatformPaths FromEnvironment() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create));
}
