// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Media.Imaging;

namespace HellboardCompanion.App.ViewModels;

/// <summary>One stratagem shown as icon with name (overview, fixed-list editor).</summary>
/// <param name="Id">Catalog id, <c>null</c> for an empty slot.</param>
/// <param name="Note">Short hint below the name, e.g. "not transferable".</param>
public sealed record StratagemTileViewModel(string? Id, string Name, Bitmap? Icon, string? Note = null)
{
    public bool HasIcon => Icon is not null;

    public bool HasNote => Note is not null;

    public static StratagemTileViewModel Empty { get; } = new(null, "empty", null);

    public override string ToString() => Name;
}

/// <summary>
/// One of the four large tiles of the overview; a click chooses the stratagem by hand, a right-click
/// sets a slot chosen by hand back to the recognized stratagem (manual-selection plan §1.1, §1.3).
/// </summary>
/// <param name="IsByHand">The stratagem was set by hand.</param>
/// <param name="InvitesChoice">Empty while nothing was recognized: shows "+" and "choose …".</param>
/// <param name="ResetTo">Name of what the recognition had in this slot ("empty" for an empty slot); <c>null</c> = nothing to go back to.</param>
public sealed record LoadoutTileViewModel(int Slot, StratagemTileViewModel Tile, bool IsByHand = false, bool InvitesChoice = false, string? ResetTo = null)
{
    public bool CanReset => ResetTo is not null;

    public string ToolTip => CanReset
        ? $"Click: choose the stratagem by hand{Environment.NewLine}Right-click: back to the recognized one ({ResetTo})"
        : "Choose the stratagem by hand";

    public string Name => InvitesChoice && Tile.Id is null ? "choose …" : Tile.Name;

    public Bitmap? Icon => Tile.Icon;

    public bool HasIcon => Tile.HasIcon;

    public string EmptyMark => InvitesChoice ? "+" : "–";

    /// <summary>"no input sequence yet" takes precedence over "set by hand".</summary>
    public string? Note => Tile.Note ?? (IsByHand ? "set by hand" : null);

    public bool HasNote => Note is not null;
}
