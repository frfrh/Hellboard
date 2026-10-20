// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellboardCompanion.App.Services;
using HellboardCompanion.Core.Hellboard;

namespace HellboardCompanion.App.ViewModels;

/// <summary>Search and category filter of the stratagem picker (manual-selection plan §1.2).</summary>
public static class StratagemPicker
{
    /// <summary>Order of the known categories; others follow alphabetically.</summary>
    private static readonly string[] CategoryOrder = ["Orbital", "Eagle", "Support weapon", "Backpack", "Vehicle", "Defensive", "Mission"];

    public static IReadOnlyList<string> OrderCategories(IEnumerable<string> categories) =>
        categories
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => Array.IndexOf(CategoryOrder, c) is var i and >= 0 ? i : CategoryOrder.Length)
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Full name, short name or id contain the search text (case-insensitive).</summary>
    public static bool Matches(CatalogEntry entry, string search)
    {
        search = search.Trim();
        return search.Length == 0
               || entry.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
               || entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
               || entry.Id.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The offered stratagems, grouped by category and sorted by short name.</summary>
    /// <param name="category"><c>null</c> = all categories.</param>
    /// <param name="excluded">Ids that are never offered, e.g. the mission stratagems for a player slot.</param>
    public static IReadOnlyList<(string Category, IReadOnlyList<CatalogEntry> Entries)> Filter(
        IEnumerable<CatalogEntry> entries, string? search, string? category, IReadOnlySet<string> excluded)
    {
        var matching = entries
            .Where(e => !excluded.Contains(e.Id) && (category is null || e.Category == category) && Matches(e, search ?? string.Empty))
            .ToList();
        return OrderCategories(matching.Select(e => e.Category))
            .Select(c => (c, (IReadOnlyList<CatalogEntry>)matching
                .Where(e => e.Category == c)
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Id, StringComparer.Ordinal)
                .ToList()))
            .ToList();
    }
}

/// <summary>One category chip; <see cref="Name"/> <c>null</c> = all categories.</summary>
public sealed partial class PickerCategoryViewModel(string? name, IBrush? dot) : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public string? Name { get; } = name;

    public string Label => Name ?? "All";

    public IBrush? Dot { get; } = dot;

    public bool HasDot => Dot is not null;
}

/// <param name="Note">"slot 2" if the stratagem is in another slot, "no input sequence", or <c>null</c>.</param>
/// <param name="IsDimmed">In another slot: choosing it swaps the slots.</param>
public sealed record PickerItemViewModel(string Id, string Name, string FullName, Bitmap? Icon, string? Note, bool IsDimmed, bool IsCurrent)
{
    public bool HasNote => Note is not null;
}

public sealed record PickerGroupViewModel(string Title, IReadOnlyList<PickerItemViewModel> Items);

/// <summary>
/// Content of the flyout at a stratagem tile of the overview: search, category chips, recently used
/// stratagems and the grid (manual-selection plan §1.2). Raises <see cref="Chosen"/> with the id, or
/// <c>null</c> for an empty slot.
/// </summary>
public sealed partial class StratagemPickerViewModel : ObservableObject
{
    private readonly StratagemCatalog _catalog;
    private readonly StratagemImages _images;
    private readonly IReadOnlyList<string?> _shown;
    private readonly IReadOnlySet<string> _excluded;
    private readonly string? _recognizedId;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showRecent;

    [ObservableProperty]
    private bool _hasMatches;

    /// <param name="shown">The shown player slots (stratagem id, <c>null</c> = empty or not recognized).</param>
    /// <param name="recognizedId">What the recognition saw in this slot before it was set by hand, if anything.</param>
    /// <param name="recent">Recently used player stratagems, newest first.</param>
    public StratagemPickerViewModel(
        StratagemCatalog catalog,
        StratagemImages images,
        IReadOnlyList<string?> shown,
        int slot,
        string? recognizedId,
        IReadOnlyList<string> recent)
    {
        _catalog = catalog;
        _images = images;
        _shown = shown;
        Slot = slot;
        _excluded = catalog.MissionOnlyIds();
        _recognizedId = recognizedId is not null && recognizedId != shown[slot] && catalog.Find(recognizedId) is not null ? recognizedId : null;

        var colors = catalog.Categories;
        Categories =
        [
            new PickerCategoryViewModel(null, null) { IsActive = true },
            .. StratagemPicker.OrderCategories(catalog.Entries.Where(e => !_excluded.Contains(e.Id)).Select(e => e.Category))
                .Select(c => new PickerCategoryViewModel(c, BrushOf(colors.GetValueOrDefault(c)))),
        ];
        Recent = recent.Select(id => catalog.Find(id)).OfType<CatalogEntry>().Where(e => !_excluded.Contains(e.Id)).Select(ToItem).ToList();
        Rebuild();
    }

    public event EventHandler<string?>? Chosen;

    /// <summary>Player slot (0–3) the picker is for.</summary>
    public int Slot { get; }

    public string Title => $"Stratagem {Slot + 1}";

    public IReadOnlyList<PickerCategoryViewModel> Categories { get; }

    public IReadOnlyList<PickerItemViewModel> Recent { get; }

    public ObservableCollection<PickerGroupViewModel> Groups { get; } = [];

    public bool CanUseRecognized => _recognizedId is not null;

    public string UseRecognizedText => _recognizedId is null ? string.Empty : $"Use recognized: {_catalog.Find(_recognizedId)?.FullName ?? _recognizedId}";

    public bool CanClear => _shown[Slot] is not null;

    partial void OnSearchTextChanged(string value) => Rebuild();

    [RelayCommand]
    private void SelectCategory(PickerCategoryViewModel category)
    {
        foreach (var chip in Categories)
        {
            chip.IsActive = ReferenceEquals(chip, category);
        }

        Rebuild();
    }

    [RelayCommand]
    private void Choose(PickerItemViewModel item) => Chosen?.Invoke(this, item.Id);

    /// <summary>Enter in the search box: the first match.</summary>
    [RelayCommand]
    private void ChooseFirst()
    {
        if (Groups.SelectMany(g => g.Items).FirstOrDefault() is { } first)
        {
            Choose(first);
        }
    }

    [RelayCommand]
    private void Clear() => Chosen?.Invoke(this, null);

    [RelayCommand]
    private void UseRecognized()
    {
        if (_recognizedId is not null)
        {
            Chosen?.Invoke(this, _recognizedId);
        }
    }

    private void Rebuild()
    {
        var category = Categories.FirstOrDefault(c => c.IsActive)?.Name;
        Groups.Clear();
        foreach (var (title, entries) in StratagemPicker.Filter(_catalog.Entries, SearchText, category, _excluded))
        {
            Groups.Add(new PickerGroupViewModel(title, entries.Select(ToItem).ToList()));
        }

        HasMatches = Groups.Count > 0;
        ShowRecent = Recent.Count > 0 && SearchText.Trim().Length == 0 && category is null;
    }

    private PickerItemViewModel ToItem(CatalogEntry entry)
    {
        var other = -1;
        for (var i = 0; i < _shown.Count; i++)
        {
            if (i != Slot && _shown[i] == entry.Id)
            {
                other = i;
            }
        }

        var note = other >= 0 ? $"slot {other + 1}" : entry.IsTransferable ? null : "no input sequence";
        return new PickerItemViewModel(entry.Id, entry.Name, entry.FullName, _images.Get(entry.Id), note, other >= 0, _shown[Slot] == entry.Id);
    }

    private static IBrush? BrushOf(string? color) => color switch
    {
        "red" => new SolidColorBrush(Color.FromRgb(0xDE, 0x7B, 0x6C)),
        "blue" => new SolidColorBrush(Color.FromRgb(0x5B, 0xA8, 0xDA)),
        "green" => new SolidColorBrush(Color.FromRgb(0x7B, 0xB8, 0x6B)),
        "yellow" => new SolidColorBrush(Color.FromRgb(0xE6, 0xC4, 0x4C)),
        "white" => new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
        _ => null,
    };
}
