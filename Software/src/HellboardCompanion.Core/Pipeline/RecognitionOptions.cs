// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Pipeline;

public enum RecognitionMode
{
    /// <summary>Only the automatic pipeline.</summary>
    Automatic,

    /// <summary>Recognition only on the hotkey; lowest CPU usage.</summary>
    ManualOnly,

    /// <summary>Automatic pipeline plus hotkey.</summary>
    AutomaticWithManualTrigger,
}

/// <summary>Settings of the recognition (implementation plan §6.13). Loaded from the configuration in P6.</summary>
public sealed class RecognitionOptions
{
    public RecognitionMode Mode { get; set; } = RecognitionMode.AutomaticWithManualTrigger;

    /// <summary>Default chosen to be pressable with one hand.</summary>
    public string TriggerHotkey { get; set; } = "Ctrl+Shift+F9";

    /// <summary>true = the manually recognized loadout is locked in like an automatic one; false = test mode.</summary>
    public bool ManualTransfer { get; set; } = true;

    /// <summary>Saves the frames of every manual trigger as PNG test data.</summary>
    public bool SaveSnapshotOnTrigger { get; set; }
}
