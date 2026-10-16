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

public sealed class GeneralOptions
{
    /// <summary>Start Hellboard Companion when the user signs in.</summary>
    public bool StartWithWindows { get; set; }
}
