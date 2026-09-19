using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Provides per-system box-art aspect ratios for correct card sizing in the game grid.
///     Keyed by system.xml SystemName. Default ratio is 1.0 for unknown systems.
/// </summary>
public class SystemArtRatioService
{
    private static readonly Dictionary<string, double> BoxRatios = new(StringComparer.OrdinalIgnoreCase)
    {
        // Atari
        ["Atari 2600"] = 0.75,
        ["Atari 5200"] = 0.75,
        ["Atari 7800"] = 0.75,
        ["Atari Jaguar"] = 0.70,
        ["Atari Jaguar CD"] = 0.70,
        ["Atari Lynx"] = 0.65,
        ["Atari ST"] = 1.0,
        ["Atari 8-Bit"] = 1.0,

        // Nintendo — consoles
        ["NES"] = 0.75,
        ["Nintendo NES"] = 0.75,
        ["Famicom"] = 0.75,
        ["Nintendo Famicom"] = 0.75,
        ["SNES"] = 1.0,
        ["Nintendo SNES"] = 1.0,
        ["Super Famicom"] = 1.0,
        ["Nintendo Super Famicom"] = 1.0,
        ["Nintendo 64"] = 0.77,
        ["Nintendo 64DD"] = 0.77,
        ["Nintendo GameCube"] = 0.70,
        ["Wii"] = 0.70,
        ["Nintendo Wii"] = 0.70,
        ["Wii U"] = 0.70,
        ["Nintendo WiiU"] = 0.70,
        ["Nintendo Switch"] = 0.71,

        // Nintendo — handhelds
        ["Game Boy"] = 0.65,
        ["Nintendo Game Boy"] = 0.65,
        ["Game Boy Color"] = 0.65,
        ["Nintendo Game Boy Color"] = 0.65,
        ["Game Boy Advance"] = 0.71,
        ["Nintendo Game Boy Advance"] = 0.71,
        ["Nintendo DS"] = 0.71,
        ["Nintendo 3DS"] = 0.71,
        ["Virtual Boy"] = 0.75,

        // Sega
        ["Sega Master System"] = 0.80,
        ["Sega Genesis"] = 0.80,
        ["Sega Mega Drive"] = 0.80,
        ["Sega Genesis CD"] = 0.70,
        ["Sega Genesis 32X"] = 0.70,
        ["Sega Saturn"] = 0.70,
        ["Sega Dreamcast"] = 0.70,
        ["Sega Game Gear"] = 0.65,
        ["Sega SG-1000"] = 0.80,
        ["Sega Mark III"] = 0.80,

        // Sony
        ["PS1"] = 0.70,
        ["Sony PlayStation 1"] = 0.70,
        ["PS2"] = 0.71,
        ["Sony PlayStation 2"] = 0.71,
        ["PS3"] = 0.71,
        ["Sony PlayStation 3"] = 0.71,
        ["PSP"] = 0.71,
        ["Sony PSP"] = 0.71,
        ["PS Vita"] = 0.71,

        // NEC
        ["PC Engine"] = 0.70,
        ["NEC PC Engine"] = 0.70,
        ["NEC PC Engine CD"] = 0.70,
        ["TurboGrafx-16"] = 0.70,
        ["NEC PC-FX"] = 0.70,
        ["NEC SuperGrafx"] = 0.70,

        // SNK
        ["Neo Geo"] = 0.70,
        ["Neo Geo CD"] = 0.70,
        ["SNK Neo Geo CD"] = 0.70,
        ["Neo Geo Pocket"] = 0.65,
        ["SNK Neo Geo Pocket"] = 0.65,
        ["Neo Geo Pocket Color"] = 0.65,
        ["SNK Neo Geo Pocket Color"] = 0.65,

        // Microsoft
        ["Microsoft Xbox"] = 0.71,
        ["Microsoft Xbox 360"] = 0.71,
        ["Microsoft MSX"] = 0.75,
        ["Microsoft MSX2"] = 0.75,

        // Arcade — landscape flyers
        ["Arcade"] = 1.41,
        ["MAME"] = 1.41,

        // Other
        ["Bandai WonderSwan"] = 0.65,
        ["Bandai WonderSwan Color"] = 0.65,
        ["Panasonic 3DO"] = 0.70,
        ["Philips CD-i"] = 0.70,
        ["Commodore 64"] = 1.0,
        ["Commodore Amiga CD32"] = 0.70,
        ["Amstrad GX4000"] = 0.75,
        ["Sinclair ZX Spectrum"] = 1.0,
        ["ColecoVision"] = 0.75,
        ["Magnavox Odyssey 2"] = 0.75,
        ["Mattel Intellivision"] = 0.75,
        ["Casio PV-1000"] = 0.75,
        ["Microsoft Windows"] = 0.71 // PC games (DVD-style)
    };

    private readonly SettingsManagerService _settings;

    public SystemArtRatioService(SettingsManagerService settings)
    {
        _settings = settings;
    }

    // Aspect ratio (height/width) applied globally, mirroring the WPF app's
    // "Set Button Aspect Ratio" menu (GameButtonFactory ratio table).
    private static double GetAspectRatioOverride(string? aspectRatio)
    {
        return aspectRatio switch
        {
            "Wider" => 1.0 / 1.5,
            "SuperWider" => 1.0 / 2.0,
            "SuperWider2" => 1.0 / 2.5,
            "Taller" => 1.3,
            "SuperTaller" => 1.6,
            "SuperTaller2" => 1.9,
            "Square" => 1.0 / 1.1,
            _ => 0.0 // no override → per-system ratio
        };
    }

    /// <summary>
    ///     Gets the box-art aspect ratio (height/width) for a given system.
    ///     Returns 1.0 for unknown systems.
    /// </summary>
    public static double GetRatio(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            return 1.0;

        return BoxRatios.GetValueOrDefault(systemName.Trim(), 1.0);
    }

    /// <summary>
    ///     Returns the art height given a card width and system name.
    ///     An explicit aspect-ratio key wins over the stored setting (used by the card
    ///     Height binding so ratio changes re-render without a library rescan).
    /// </summary>
    public double GetArtHeight(double cardWidth, string systemName, bool isMixedView = false,
        string? aspectRatioOverride = null)
    {
        // The "Set Button Aspect Ratio" setting drives card sizing globally
        // (same as the WPF GameButtonFactory). Unknown values fall back to the
        // per-system art ratio for the current view.
        var overrideRatio = GetAspectRatioOverride(aspectRatioOverride ?? _settings.ButtonAspectRatio);
        var ratio = overrideRatio > 0.0 ? overrideRatio :
            isMixedView ? 0.73 : GetRatio(systemName);
        return cardWidth * ratio;
    }
}