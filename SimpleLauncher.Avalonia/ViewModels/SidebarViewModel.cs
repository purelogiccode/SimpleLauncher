using System.Collections.ObjectModel;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     Builds and updates the sidebar system list from system.xml data.
///     All configured systems are listed flat (no manufacturer headers), so a system
///     added through Easy/Expert Mode appears as soon as Populate is called again.
/// </summary>
public class SidebarViewModel
{
    /// <summary>
    ///     The flat list of all systems for the sidebar (system.xml order).
    /// </summary>
    public ObservableCollection<SidebarSystemItem> Systems { get; } = [];

    /// <summary>
    ///     Builds the system items from system.xml data. Idempotent — clears and refills the list.
    ///     When an image resolver is supplied, icons are resolved with annotation-stripped
    ///     and fuzzy matching (WPF SystemImageResolverService parity); otherwise only the
    ///     exact "images/systems/{name}.png" file is used.
    /// </summary>
    public void Populate(IEnumerable<SystemManagerConfig> systems, ISystemImageResolverService? imageResolver = null)
    {
        Systems.Clear();

        foreach (var system in systems)
        {
            var iconPath = imageResolver is not null
                ? imageResolver.ResolveSystemIcon(system.SystemName)
                : ExactPngIcon(system.SystemName);

            Systems.Add(new SidebarSystemItem
            {
                SystemName = system.SystemName,
                IconPath = iconPath
            });
        }
    }

    private static string? ExactPngIcon(string systemName)
    {
        var iconPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "images", "systems", systemName + ".png");
        return File.Exists(iconPath) ? iconPath : null;
    }

    /// <summary>
    ///     Updates the count badges for all systems from the live counts dictionary.
    /// </summary>
    public void RefreshCounts(IReadOnlyDictionary<string, int> counts)
    {
        foreach (var item in Systems) item.Count = counts.GetValueOrDefault(item.SystemName, 0);
    }
}