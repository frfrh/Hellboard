// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Platform.Windows;

/// <summary>
/// Windows 11 is fully supported; Windows 10 only as far as it does not block or complicate
/// the implementation (implementation plan §3.2).
/// </summary>
public sealed class WindowsPlatformInfo : IPlatformInfo
{
    private const int Windows11Build = 22000;

    public WindowsPlatformInfo()
    {
        var build = Environment.OSVersion.Version.Build;
        var isWindows11 = System.OperatingSystem.IsWindowsVersionAtLeast(10, 0, Windows11Build);

        OperatingSystem = $"{(isWindows11 ? "Windows 11" : "Windows 10")} (build {build})";
        SupportLevel = isWindows11 ? PlatformSupportLevel.Full : PlatformSupportLevel.BestEffort;
        SupportNote = isWindows11
            ? null
            : "Windows 10 is supported on a best-effort basis. Some features may be limited, e.g. the yellow screen capture border cannot always be hidden.";
    }

    public string OperatingSystem { get; }

    public PlatformSupportLevel SupportLevel { get; }

    public string? SupportNote { get; }
}
