// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Layout;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Recognition;

public enum ReadyStatus
{
    /// <summary>Neither state is clear, e.g. during the transition animation.</summary>
    Unknown,

    /// <summary>Dark button with yellow stripes: the loadout can still be changed.</summary>
    Prepare,

    /// <summary>Yellow button: the player has confirmed the loadout.</summary>
    Ready,
}

public sealed record ReadyStatusResult(ReadyStatus Status, double YellowShare);

/// <summary>
/// Classifies the ready button by its share of yellow pixels (implementation plan §6.4). The label
/// is ignored, so the game language does not matter. The inner area excludes the yellow focus outline.
/// </summary>
public sealed class ReadyStatusDetector(LoadoutLayout layout)
{
    public const double PrepareMaxShare = 0.35;
    public const double ReadyMinShare = 0.60;

    private const double InsetX = 0.05;
    private const double InsetY = 0.15;

    /// <param name="frame">Capture of the game's client area.</param>
    /// <param name="offset">Correction of the layout position found by the screen detection, in pixels.</param>
    public ReadyStatusResult Detect(Frame frame, PixelOffset offset = default)
    {
        var scaler = new LayoutScaler(layout, frame.ScreenBounds);
        var button = scaler.ToScreen(layout.ReadyButton);
        var dx = (int)Math.Round(button.Width * InsetX);
        var dy = (int)Math.Round(button.Height * InsetY);
        var inner = new PixelRect(
            button.X - frame.ScreenBounds.X + dx + offset.X,
            button.Y - frame.ScreenBounds.Y + dy + offset.Y,
            button.Width - (2 * dx),
            button.Height - (2 * dy));

        var share = frame.Share(inner, IsYellow);
        var status = share switch
        {
            <= PrepareMaxShare => ReadyStatus.Prepare,
            >= ReadyMinShare => ReadyStatus.Ready,
            _ => ReadyStatus.Unknown,
        };

        return new ReadyStatusResult(status, share);
    }

    private static bool IsYellow(byte b, byte g, byte r) => r > 180 && g > 160 && b < 90;
}
