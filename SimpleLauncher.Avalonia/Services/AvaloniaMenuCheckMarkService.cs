using Avalonia.Controls;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Manages the checked state of option menu items (thumbnail size, games per page,
///     show-games filter, button aspect ratio, filename display mode, filename font size,
///     machine-name font size, view mode). The Avalonia menu items carry their value in
///     <c>Tag</c> (sizes / page counts) or <c>Name</c> (aspect ratios / display modes),
///     so the service can drive any menu generically — the same 8-category contract the
///     WPF <c>MenuCheckMarkService</c> provides through its named host properties.
/// </summary>
public static class AvaloniaMenuCheckMarkService
{
    /// <summary>
    ///     Checks exactly the menu item whose <c>Tag</c> parses to <paramref name="selectedValue" />
    ///     (used by the Button Size and Games Per Page menus).
    /// </summary>
    /// <param name="menuItems">The menu items of the submenu.</param>
    /// <param name="selectedValue">The selected numeric value.</param>
    public static void UpdateCheckedByTag(IEnumerable<MenuItem> menuItems, int selectedValue)
    {
        foreach (var item in menuItems)
        {
            item.IsChecked = item.Tag is string tag &&
                             int.TryParse(tag, System.Globalization.CultureInfo.InvariantCulture, out var tagValue) &&
                             tagValue == selectedValue;
        }
    }

    /// <summary>
    ///     Checks exactly the menu item whose <c>Name</c> equals <paramref name="selectedName" />
    ///     (used by the Button Aspect Ratio menu).
    /// </summary>
    /// <param name="menuItems">The menu items of the submenu.</param>
    /// <param name="selectedName">The selected item name.</param>
    public static void UpdateCheckedByName(IEnumerable<MenuItem> menuItems, string? selectedName)
    {
        foreach (var item in menuItems)
            item.IsChecked = string.Equals(item.Name, selectedName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Updates the show-games filter check marks (ShowAll / ShowWithCover / ShowWithoutCover).
    /// </summary>
    public static void UpdateShowGamesCheckMarks(IEnumerable<MenuItem> menuItems, string? selectedValue)
    {
        UpdateCheckedByName(menuItems, selectedValue);
    }

    /// <summary>
    ///     Updates the filename display mode check marks (Original / CleanUp / NoFilename).
    ///     WPF stores "Original"/"CleanUp"/"NoFilename" while the Avalonia menu items are named
    ///     "FilenameDisplayOriginal" etc., so the check must map the value to the prefixed name
    ///     (mirrors WPF MenuCheckMarkService.UpdateFilenameDisplayModeCheckMarks).
    /// </summary>
    public static void UpdateFilenameDisplayModeCheckMarks(IEnumerable<MenuItem> menuItems, string? selectedValue)
    {
        var targetName = selectedValue is null ? null : "FilenameDisplay" + selectedValue;
        foreach (var item in menuItems) item.IsChecked = string.Equals(item.Name, targetName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Updates the filename font size check marks (Small / Normal / Big).
    ///     WPF value is "Small"/"Normal"/"Big" while the Avalonia items are named
    ///     "FilenameFontSizeSmall" etc., so map to the prefixed name.
    /// </summary>
    public static void UpdateFilenameFontSizeCheckMarks(IEnumerable<MenuItem> menuItems, string? selectedValue)
    {
        var targetName = selectedValue is null ? null : "FilenameFontSize" + selectedValue;
        foreach (var item in menuItems) item.IsChecked = string.Equals(item.Name, targetName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Updates the machine-name font size check marks (Small / Normal / Big).
    /// </summary>
    public static void UpdateMachineNameFontSizeCheckMarks(IEnumerable<MenuItem> menuItems, string? selectedValue)
    {
        var targetName = selectedValue is null ? null : "MachineNameFontSize" + selectedValue;
        foreach (var item in menuItems) item.IsChecked = string.Equals(item.Name, targetName, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Sets the view-mode check marks (GridView / ListView) to reflect the active view.
    /// </summary>
    public static void SetViewModeCheckMarks(MenuItem gridViewItem, MenuItem listViewItem, bool isGridView)
    {
        gridViewItem.IsChecked = isGridView;
        listViewItem.IsChecked = !isGridView;
    }
}