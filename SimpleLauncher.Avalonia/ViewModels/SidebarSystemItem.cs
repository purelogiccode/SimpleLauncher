using CommunityToolkit.Mvvm.ComponentModel;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     A single system entry in the sidebar (icon + name + live count badge).
/// </summary>
internal partial class SidebarSystemItem : ObservableObject
{
    [ObservableProperty] public partial int Count { get; set; }

    /// <summary>System name — used as the navigation tag.</summary>
    public string SystemName { get; init; } = "";

    /// <summary>Resolved path to the system icon PNG, or null when no icon exists.</summary>
    public string? IconPath { get; init; }

    /// <summary>Whether a system icon file exists (drives image vs glyph fallback).</summary>
    public bool HasIcon => IconPath is not null;

    /// <summary>Formatted count badge text (empty when zero, like the original UI).</summary>
    public string CountText => Count > 0 ? $"  {Count}" : "";

    partial void OnCountChanged(int value)
    {
        _ = value; // Parameter is required by the generated partial method signature.
        OnPropertyChanged(nameof(CountText));
    }
}