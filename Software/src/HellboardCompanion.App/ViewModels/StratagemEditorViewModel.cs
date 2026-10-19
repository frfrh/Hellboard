// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellboardCompanion.App.Services;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HellboardCompanion.App.ViewModels;

/// <summary>One stratagem in the editor list.</summary>
/// <param name="Name">Short name, sent to the Hellboard and used for sorting.</param>
/// <param name="FullName">Full name (wiki designation) shown in the Companion.</param>
public sealed record StratagemEditorItem(string Id, string Name, string FullName, string Category, string? Code, string Source, Bitmap? Icon, string? SvgPath,
    int ReferenceCount = 0)
{
    public bool HasReference => ReferenceCount > 0;

    /// <summary>Origin of the entry for the list, e.g. "Built-in, with reference".</summary>
    public string SourceText => HasReference ? $"{Source}, with reference" : Source;

    public string ReferenceText => ReferenceCount == 0
        ? "None. If the game shows this stratagem differently from its icon, assign an unrecognized slot to it in the History tab."
        : $"{ReferenceCount} slot image(s) from the game – the recognition uses them instead of the icon.";

    public string CodeArrows => StratagemEditorViewModel.ToArrows(Code) is { Length: > 0 } arrows ? arrows : "no code yet";

    public bool HasCode => !string.IsNullOrEmpty(Code);

    public bool HasIcon => Icon is not null;
}

/// <summary>
/// Stratagems tab (implementation plan §6.12, P8): input codes of new stratagems and corrections of
/// existing ones. Changes are stored in the user's own list (they survive releases and icon updates) and
/// apply immediately: the catalog is reloaded and the current loadout is sent again if it changed.
/// </summary>
public sealed partial class StratagemEditorViewModel : ObservableObject, IDisposable
{
    private readonly StratagemCatalog _catalog;
    private readonly StratagemIconStore? _icons;
    private readonly StratagemImages _images;
    private readonly StratagemReferenceStore? _references;
    private readonly ILogger _logger;
    private readonly StratagemCatalog _builtIn = StratagemCatalog.LoadSeed();
    private List<StratagemEditorItem> _all = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _onlyWithoutCode;

    [ObservableProperty]
    private bool _onlyWithReference;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveReferencesCommand))]
    private StratagemEditorItem? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _editName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _editCategory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditCodeArrows))]
    [NotifyPropertyChangedFor(nameof(CodeProblem))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _editCode = string.Empty;

    [ObservableProperty]
    private string? _result;

    public StratagemEditorViewModel(StratagemCatalog catalog, StratagemIconStore? icons, StratagemImages images, ILogger? logger = null,
        StratagemReferenceStore? references = null)
    {
        _catalog = catalog;
        _icons = icons;
        _images = images;
        _references = references;
        _logger = logger ?? NullLogger.Instance;
        _catalog.Changed += OnCatalogChanged;
        if (_icons is not null)
        {
            _icons.IconsChanged += OnCatalogChanged;
        }

        if (_references is not null)
        {
            _references.Changed += OnCatalogChanged;
        }

        Rebuild();
    }

    public ObservableCollection<StratagemEditorItem> Items { get; } = [];

    public IReadOnlyList<string> Categories => _catalog.Categories.Keys.Order(StringComparer.Ordinal).ToList();

    public bool HasSelection => Selected is not null;

    public string EditCodeArrows => ToArrows(EditCode) is { Length: > 0 } arrows ? arrows : "–";

    /// <summary>Length problem or a code that another stratagem already uses.</summary>
    public string? CodeProblem
    {
        get
        {
            if (EditCode.Length is > 0 and < UserStratagemList.MinCodeLength)
            {
                return $"A code has at least {UserStratagemList.MinCodeLength} arrows.";
            }

            var others = _catalog.Entries.Where(e => e.Sequence == EditCode && e.Id != Selected?.Id).Select(e => e.Name).ToList();
            return EditCode.Length > 0 && others.Count > 0 ? $"Warning: the same code is used by {string.Join(", ", others)}." : null;
        }
    }

    public string? UserFile => _catalog.UserFilePath;

    public string Summary => $"{_all.Count} stratagems, {_all.Count(i => !i.HasCode)} without code, {_all.Count(i => i.HasReference)} with reference, {_catalog.UserIds.Count} in your list";

    // ---- code entry -----------------------------------------------------------------------------

    [RelayCommand]
    private void AddArrow(string direction)
    {
        if (Selected is not null && EditCode.Length < UserStratagemList.MaxCodeLength && direction is "U" or "D" or "L" or "R")
        {
            EditCode += direction;
        }
    }

    [RelayCommand]
    private void RemoveArrow()
    {
        if (EditCode.Length > 0)
        {
            EditCode = EditCode[..^1];
        }
    }

    [RelayCommand]
    private void ClearCode() => EditCode = string.Empty;

    // ---- saving ---------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (Selected is not { } item || UserFile is not { } path || EditCategory is null)
        {
            return;
        }

        var entry = new UserStratagem(item.Id, EditName.Trim(), EditCategory, EditCode.Length > 0 ? EditCode : null);
        if (UserStratagemList.Validate(entry, _catalog.Categories.Keys.ToList()) is { } error)
        {
            Result = error;
            return;
        }

        try
        {
            var list = UserStratagemList.Load(path);
            var builtIn = _builtIn.Find(item.Id);
            if (builtIn is not null && builtIn.Name == entry.Name && builtIn.Category == entry.Category && builtIn.Sequence == entry.Code)
            {
                list.Remove(item.Id);   // same as the built-in entry: no override needed
            }
            else
            {
                list.Set(entry);
            }

            list.Save();
            Reload(path);
            Result = entry.Code is null
                ? $"{entry.Name} saved without code – it cannot be sent to the Hellboard yet."
                : $"{entry.Name} saved – it is sent to the Hellboard from now on.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            Result = $"Not saved: {ex.Message}";
        }
    }

    private bool CanSave() => Selected is not null && UserFile is not null && !string.IsNullOrWhiteSpace(EditName) && EditCategory is not null
        && EditCode.Length is 0 or >= UserStratagemList.MinCodeLength;

    /// <summary>Removes your change of a built-in stratagem, or your own stratagem.</summary>
    [RelayCommand(CanExecute = nameof(CanReset))]
    private void Reset()
    {
        if (Selected is not { } item || UserFile is not { } path)
        {
            return;
        }

        try
        {
            var list = UserStratagemList.Load(path);
            list.Remove(item.Id);
            list.Save();
            Reload(path);
            Result = _builtIn.Find(item.Id) is not null ? $"{item.Name}: built-in code restored." : $"{item.Name} removed from your list.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            Result = $"Not changed: {ex.Message}";
        }
    }

    private bool CanReset() => Selected is not null && _catalog.UserIds.Contains(Selected.Id);

    [RelayCommand(CanExecute = nameof(CanRemoveReferences))]
    private void RemoveReferences()
    {
        if (Selected is not { } item || _references is null)
        {
            return;
        }

        try
        {
            _references.Remove(item.Id);   // → Changed → Rebuild, the recognizer is rebuilt
            Result = $"{item.Name}: reference images removed.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Result = $"Not removed: {ex.Message}";
        }
    }

    private bool CanRemoveReferences() => Selected is { ReferenceCount: > 0 } && _references is not null;

    /// <summary>Writes your list to <paramref name="target"/> (to share it).</summary>
    public void ExportTo(string target)
    {
        try
        {
            UserStratagemList.Load(UserFile!).Export(target);
            Result = $"Your list was exported to {target}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or NullReferenceException)
        {
            Result = $"Not exported: {ex.Message}";
        }
    }

    /// <summary>Merges the stratagems of <paramref name="source"/> into your list.</summary>
    public void ImportFrom(string source)
    {
        if (UserFile is not { } path)
        {
            return;
        }

        try
        {
            var list = UserStratagemList.Load(path);
            var count = list.Import(source);
            foreach (var entry in list.Entries)
            {
                if (UserStratagemList.Validate(entry, [.. _catalog.Categories.Keys, .. list.Categories.Keys]) is { } error)
                {
                    Result = $"Not imported – {entry.Id}: {error}";
                    return;
                }
            }

            list.Save();
            Reload(path);
            Result = $"{count} stratagems imported.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            Result = $"Not imported: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _catalog.Changed -= OnCatalogChanged;
        if (_icons is not null)
        {
            _icons.IconsChanged -= OnCatalogChanged;
        }

        if (_references is not null)
        {
            _references.Changed -= OnCatalogChanged;
        }
    }

    public static string ToArrows(string? code) => string.Concat((code ?? string.Empty).Select(c => c switch
    {
        'U' => "↑",
        'D' => "↓",
        'L' => "←",
        'R' => "→",
        _ => "?",
    }));

    partial void OnSearchTextChanged(string value) => Filter();

    partial void OnOnlyWithoutCodeChanged(bool value) => Filter();

    partial void OnOnlyWithReferenceChanged(bool value) => Filter();

    partial void OnSelectedChanged(StratagemEditorItem? value)
    {
        Result = null;
        EditName = value?.Name ?? string.Empty;
        EditCategory = value?.Category is { Length: > 0 } category ? category : null;
        EditCode = value?.Code ?? string.Empty;
    }

    private void Reload(string path)
    {
        var selectedId = Selected?.Id;
        _catalog.ReplaceWith(StratagemCatalog.Load(path, _logger));   // → Changed → Rebuild, Hellboard re-sends
        Selected = Items.FirstOrDefault(i => i.Id == selectedId);
    }

    private void OnCatalogChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        var selectedId = Selected?.Id;
        var result = Result;
        Rebuild();
        Selected = Items.FirstOrDefault(i => i.Id == selectedId);
        Result = result;
    });

    private void Rebuild()
    {
        var icons = _icons?.Icons ?? [];
        var items = _catalog.Entries.Select(e => new StratagemEditorItem(
            e.Id, e.Name, e.FullName, e.Category, e.Sequence, SourceOf(e.Id), _images.Get(e.Id), icons.FirstOrDefault(i => i.Id == e.Id)?.SvgPath,
            _references?.Count(e.Id) ?? 0)).ToList();
        foreach (var icon in icons.Where(i => _catalog.Find(i.Id) is null))
        {
            items.Add(new StratagemEditorItem(icon.Id, icon.Name, icon.Name, GuessCategory(icon.SvgPath), null, "New icon – no code yet", _images.Get(icon.Id), icon.SvgPath,
                _references?.Count(icon.Id) ?? 0));
        }

        RemoveReferencesCommand.NotifyCanExecuteChanged();

        _all = items.OrderBy(i => i.HasCode).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        OnPropertyChanged(nameof(Categories));
        OnPropertyChanged(nameof(Summary));
        ResetCommand.NotifyCanExecuteChanged();
        Filter();
    }

    private string SourceOf(string id) => (_catalog.UserIds.Contains(id), _builtIn.Find(id) is not null) switch
    {
        (true, true) => "Built-in, changed by you",
        (true, false) => "Your own stratagem",
        _ => "Built-in",
    };

    private void Filter()
    {
        var search = SearchText.Trim();
        Items.Clear();
        foreach (var item in _all.Where(i => (!OnlyWithoutCode || !i.HasCode) && (!OnlyWithReference || i.HasReference)
                     && (search.Length == 0 || i.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) || i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || i.Id.Contains(search, StringComparison.OrdinalIgnoreCase))))
        {
            Items.Add(item);
        }
    }

    /// <summary>The category whose LED color matches the color of the icon (red, blue, green, yellow).</summary>
    private string GuessCategory(string? svgPath)
    {
        var fallback = Categories.Contains("Mission") ? "Mission" : Categories.FirstOrDefault() ?? string.Empty;
        if (svgPath is null || !File.Exists(svgPath))
        {
            return fallback;
        }

        foreach (Match match in ColorPattern().Matches(File.ReadAllText(svgPath)))
        {
            var rgb = Convert.ToInt32(match.Groups[1].Value, 16);
            int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) < 40)
            {
                continue;   // gray, white or black details
            }

            var color = r > 150 && g > 120 && b < 100 ? "yellow" : r >= g && r >= b ? "red" : g >= b ? "green" : "blue";
            var preferred = color switch { "red" => "Orbital", "blue" => "Support weapon", "green" => "Defensive", _ => "Mission" };
            return _catalog.Categories.TryGetValue(preferred, out var c) && c == color ? preferred
                : _catalog.Categories.FirstOrDefault(kv => kv.Value == color).Key ?? fallback;
        }

        return fallback;
    }

    [GeneratedRegex("#([0-9a-fA-F]{6})\\b")]
    private static partial Regex ColorPattern();
}
