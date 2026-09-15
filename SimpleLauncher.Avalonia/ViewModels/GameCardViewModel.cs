using CommunityToolkit.Mvvm.ComponentModel;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     Represents a single game card in the grid.
///     Mapped from scan results + cache + favorites + play history.
/// </summary>
public partial class GameCardViewModel : ObservableObject
{
    /// <summary>
    ///     Known RetroAchievements-supported systems (by system.xml name).
    /// </summary>
    private static readonly HashSet<string> RaSupportedSystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "NES", "Nintendo NES", "Famicom", "Nintendo Famicom",
        "SNES", "Nintendo SNES", "Super Famicom", "Nintendo Super Famicom",
        "Nintendo 64", "N64",
        "Nintendo GameCube", "GameCube",
        "Wii", "Nintendo Wii", "Wii U", "Nintendo WiiU",
        "Game Boy", "Nintendo Game Boy",
        "Game Boy Color", "Nintendo Game Boy Color",
        "Game Boy Advance", "Nintendo Game Boy Advance", "GBA",
        "Nintendo DS", "DS", "Nintendo 3DS", "3DS",
        "Sega Genesis", "Sega Mega Drive", "Genesis",
        "Sega Master System", "Master System",
        "Sega Saturn", "Saturn",
        "Sega Dreamcast", "Dreamcast",
        "Sega Game Gear", "Game Gear",
        "Sega CD", "Sega Genesis CD",
        "Sega 32X", "Sega Genesis 32X",
        "PS1", "Sony PlayStation 1", "PlayStation",
        "PS2", "Sony PlayStation 2",
        "PSP", "Sony PSP",
        "Arcade", "MAME",
        "PC Engine", "NEC PC Engine", "TurboGrafx-16",
        "NEC PC Engine CD", "NEC PC-FX",
        "Neo Geo", "Neo Geo CD", "SNK Neo Geo CD",
        "Neo Geo Pocket", "SNK Neo Geo Pocket",
        "Atari 2600", "Atari 5200", "Atari 7800",
        "Atari Jaguar", "Atari Lynx",
        "Commodore 64", "ColecoVision",
        "MSX", "Microsoft MSX", "Microsoft MSX2",
        "WonderSwan", "Bandai WonderSwan", "Bandai WonderSwan Color",
        "Panasonic 3DO", "3DO",
        "Philips CD-i"
    };

    [ObservableProperty] public partial string CoverPath { get; set; } = "";

    [ObservableProperty] public partial string DisplayTitle { get; set; } = "";

    [ObservableProperty] public partial string FileName { get; set; } = "";

    [ObservableProperty] public partial string FilePath { get; set; } = "";

    [ObservableProperty] public partial string FolderPath { get; set; } = "";

    [ObservableProperty] public partial bool HasCover { get; set; }

    [ObservableProperty] public partial bool IsFavorite { get; set; }

    /// <summary>
    ///     Whether this game's system is supported by RetroAchievements.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRaSupported { get; set; }

    [ObservableProperty] public partial string? LastPlayed { get; set; }

    [ObservableProperty] public partial string MachineDescription { get; set; } = "";

    [ObservableProperty] public partial string PlaceholderColor { get; set; } = "#15FFFFFF";

    [ObservableProperty] public partial int PlayCount { get; set; }

    [ObservableProperty] public partial string PlayTime { get; set; } = "0m 0s";

    [ObservableProperty] public partial int? Rating { get; set; }

    [ObservableProperty] public partial string SystemName { get; set; } = "";

    [ObservableProperty] public partial string TimesPlayed { get; set; } = "0";

    partial void OnSystemNameChanged(string value)
    {
        Log.Debug("Game card created for {GameName} ({SystemName}) at {FilePath}", FileName, value, FilePath);
    }

    partial void OnCoverPathChanged(string value)
    {
        Log.Debug("Cover image {CoverState} for {GameName} ({SystemName}): {CoverPath}",
            File.Exists(value) ? "found" : "missing", FileName, SystemName, value);
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        Log.Debug("Favorite badge {BadgeState} for {GameName} ({SystemName})",
            value ? "shown" : "hidden", FileName, SystemName);
    }

    partial void OnIsRaSupportedChanged(bool value)
    {
        Log.Debug("RetroAchievements badge {BadgeState} for {GameName} ({SystemName})",
            value ? "shown" : "hidden", FileName, SystemName);
    }

    public static bool IsSystemRaSupported(string systemName)
    {
        return !string.IsNullOrEmpty(systemName) && RaSupportedSystems.Contains(systemName);
    }
}