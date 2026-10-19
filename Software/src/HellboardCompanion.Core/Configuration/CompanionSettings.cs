// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Configuration;

/// <summary>
/// All user settings (implementation plan §6.8), stored as <c>config.json</c> in the configuration
/// directory. The sections are the option objects the services use directly, so a change is visible
/// to them immediately; services that need more than reading a value react to <see cref="SettingsStore.Saved"/>.
/// </summary>
public sealed class CompanionSettings
{
    public GeneralOptions General { get; set; } = new();

    public GameLocatorOptions Game { get; set; } = new();

    public RecognitionOptions Recognition { get; set; } = new();

    public HellboardOptions Hellboard { get; set; } = new();

    public MultiFunctionOptions MultiFunction { get; set; } = new();

    public BoardOptions Board { get; set; } = new();

    public UiOptions Ui { get; set; } = new();
}

public enum HistoryFilter
{
    /// <summary>Loadouts locked by Ready or manually, and problems – what matters to the user.</summary>
    Important,

    /// <summary>Everything, including lock-ins when the screen closed.</summary>
    All,
}

public sealed class UiOptions
{
    public HistoryFilter HistoryFilter { get; set; } = HistoryFilter.Important;
}

/// <summary>What the close button (X) of the window does.</summary>
public enum CloseAction
{
    /// <summary>The window closes, the Companion keeps running in the system tray.</summary>
    KeepRunningInTray,

    /// <summary>The Companion exits.</summary>
    Exit,
}

public sealed class GeneralOptions
{
    /// <summary>Start Hellboard Companion when the user signs in.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Start with only the tray icon instead of opening the window.</summary>
    public bool StartMinimized { get; set; }

    public CloseAction CloseAction { get; set; } = CloseAction.KeepRunningInTray;

    /// <summary>Look for a newer Companion release on GitHub at start.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Release the user chose to skip ("0.8.0"); a later release is offered again.</summary>
    public string? SkippedVersion { get; set; }
}
