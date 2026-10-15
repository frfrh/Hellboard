// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>
/// Per-user directories of the application. Never hard-code platform-specific paths elsewhere.
/// </summary>
public interface IPlatformPaths
{
    /// <summary>User configuration (Windows: <c>%APPDATA%\HellboardCompanion</c>, Linux: <c>~/.config/HellboardCompanion</c>).</summary>
    string ConfigDirectory { get; }

    /// <summary>Local data such as catalog, templates and history (Windows: <c>%LOCALAPPDATA%\HellboardCompanion</c>, Linux: <c>~/.local/share/HellboardCompanion</c>).</summary>
    string DataDirectory { get; }

    /// <summary>Rolling log files, below <see cref="DataDirectory"/>.</summary>
    string LogDirectory { get; }
}
