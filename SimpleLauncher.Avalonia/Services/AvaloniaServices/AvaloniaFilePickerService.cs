using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Services.AvaloniaServices;

/// <summary>
///     Avalonia implementation of IFilePickerService — provides async file/folder dialogs
///     via the platform StorageProvider.
/// </summary>
public class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> OpenFileAsync(string title, string filter = "All files|*.*")
    {
        var topLevel = GetOwnerWindow();
        if (topLevel is null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = ParseFilter(filter)
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> OpenFolderAsync(string title)
    {
        var topLevel = GetOwnerWindow();
        if (topLevel is null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> SaveFileAsync(string title, string filter = "All files|*.*")
    {
        var topLevel = GetOwnerWindow();
        if (topLevel is null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = ParseFilter(filter)
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>
    ///     Resolves the window a picker dialog should be modal to. The currently active window
    ///     wins so dialogs opened from child windows (RA settings, Edit System, image pack) are
    ///     parented to the caller instead of always the MainWindow — on Wayland/GNOME the
    ///     portal can otherwise appear behind the active window (LB-21).
    /// </summary>
    private static Window? GetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        return desktop.Windows.FirstOrDefault(static window => window.IsActive) ?? desktop.MainWindow;
    }

    /// <summary>
    ///     Parses a WPF-style filter string ("Description|*.ext1;*.ext2") into storage file types.
    ///     Internal so tests can assert that the platform-aware emulator filters do not hide
    ///     Unix binaries.
    /// </summary>
    internal static IReadOnlyList<FilePickerFileType>? ParseFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return null;

        var parts = filter.Split('|');
        var types = new List<FilePickerFileType>();
        var specificFilters = 0;

        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var name = parts[i].Trim();
            var patterns = parts[i + 1]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (patterns.Count == 0) continue;

            // "All files|*.*" becomes an unrestricted entry using Avalonia's all-files pattern
            // ("*"), so a list such as "MP3 files|*.mp3|All files|*.*" keeps both the specific
            // filter and the option to pick any file (LB-10). Returning null here used to drop
            // the whole list before this was parsed, and skipping the pair entirely removed the
            // unrestricted option the WPF picker still offers.
            if (patterns.All(p => string.Equals(p, "*.*", StringComparison.Ordinal)))
            {
                types.Add(new FilePickerFileType(name) { Patterns = ["*"] });
                continue;
            }

            types.Add(new FilePickerFileType(name) { Patterns = patterns });
            specificFilters++;
        }

        // Only an all-files entry (or nothing at all): return no filter so the platform shows
        // its default complete file dialog.
        return specificFilters > 0 ? types : null;
    }
}