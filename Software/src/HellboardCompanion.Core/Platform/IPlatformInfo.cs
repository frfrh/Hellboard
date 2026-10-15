// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>
/// Describes the operating system the application runs on and how well it is supported
/// (implementation plan §3.2).
/// </summary>
public interface IPlatformInfo
{
    /// <summary>Human-readable OS description, e.g. "Windows 11 (build 26200)".</summary>
    string OperatingSystem { get; }

    PlatformSupportLevel SupportLevel { get; }

    /// <summary>Explanation shown to the user when the support level is not <see cref="PlatformSupportLevel.Full"/>.</summary>
    string? SupportNote { get; }
}

public enum PlatformSupportLevel
{
    Full,
    BestEffort,
    Unsupported,
}
