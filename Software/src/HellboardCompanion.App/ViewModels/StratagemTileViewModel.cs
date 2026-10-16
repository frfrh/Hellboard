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
