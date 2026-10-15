// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Recognition;

namespace HellboardCompanion.App.ViewModels;

/// <summary>User-facing texts for manual recognitions and file analyses.</summary>
public static class ManualRecognitionText
{
    public static (string Title, string Body) Describe(ManualRecognitionResult result)
    {
        var title = result switch
        {
            { Loadout: null } => "Recognition failed",
            { Locked: true } => "Loadout locked in",
            { TestMode: true } => "Loadout recognized (test mode)",
            _ => "Loadout not locked in",
        };

        var lines = new List<string>();
        if (result.Loadout is { } loadout)
        {
            lines.Add(Describe(loadout));
            lines.Add($"Ready button: {result.Status}");
        }

        if (result.Problem is not null)
        {
            lines.Add(result.Problem);
        }

        if (!result.ScreenDetected && result.Screen is { } screen)
        {
            lines.Add($"Screen scores: button {screen.ButtonScore:F2}, slots {screen.GridScore:F2}, mission frames {screen.MissionFrameScore:F2}");
        }

        if (result.SavedFiles.Count > 0)
        {
            lines.Add($"{result.SavedFiles.Count} frames saved to {Path.GetDirectoryName(result.SavedFiles[0])}");
        }

        return (title, string.Join(Environment.NewLine, lines));
    }

    public static string Describe(LoadoutObservation observation)
    {
        var lines = new List<string>
        {
            observation.ScreenVisible ? "Loadout screen detected" : "Loadout screen NOT detected – slots read at their layout positions",
            $"Ready button: {observation.Status}",
            $"Screen scores: button {observation.Screen.ButtonScore:F2}, slots {observation.Screen.GridScore:F2}, mission frames {observation.Screen.MissionFrameScore:F2}",
        };

        if (observation.Loadout is { } loadout)
        {
            lines.Add(Describe(loadout));
        }

        lines.Add($"Analyzed in {observation.Duration.TotalMilliseconds:F0} ms");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Describe(LoadoutSnapshot loadout) =>
        $"Stratagems: {string.Join(", ", loadout.PlayerSlots)}{Environment.NewLine}Mission: {string.Join(", ", loadout.MissionSlots)}";
}
