using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.RetroAchievements;

/// <summary>
///     Provides fuzzy matching for RetroAchievements system names to ensure proper hash method selection.
/// </summary>
public class RetroAchievementsSystemMatcher : IRetroAchievementsSystemMatcher
{
    // Define system name mappings with their official RA Console ID and fuzzy matching patterns.
    private static readonly Dictionary<string, RaSystemInfo> SystemMappings = new(StringComparer.Ordinal)
    {
        // Nintendo systems
        ["nintendo 64"] = new RaSystemInfo(2, ["nintendo 64", "n64", "nintendo64", "Nintendo 64DD", "Nintendo N64"]),
        ["super nintendo entertainment system"] = new RaSystemInfo(3,
        [
            "super nintendo entertainment system", "snes", "super nintendo", "super famicom", "Nintendo SNES",
            "Nintendo SFC", "Nintendo Super Famicom", "NintendoSNES", "Nintendo Satellaview"
        ]),
        ["game boy"] = new RaSystemInfo(4, ["game boy", "gb", "gameboy", "Nintendo Gameboy", "Nintendo Game Boy"]),
        ["game boy advance"] = new RaSystemInfo(5,
        [
            "game boy advance", "gba", "gameboy advance", "nintendo gameboy advance", "Nintendo GBA",
            "Nintendo Game Boy Advance", "Nintendo - Game Boy Advance"
        ]),
        ["game boy color"] = new RaSystemInfo(6,
        [
            "game boy color", "gbc", "gameboy color", "nintendo gameboy color", "Nintendo - Game Boy Color",
            "Nintendo Game Boy Color"
        ]),
        ["nintendo entertainment system"] = new RaSystemInfo(7,
            ["nintendo entertainment system", "nes", "famicom", "nintendo nes", "Nintendo Famicom"]),
        ["gamecube"] = new RaSystemInfo(16,
        [
            "gamecube", "game cube", "game-cube", "gc", "nintendo gamecube", "nintendo game cube", "nintendo game-cube",
            "nintendo gc", "Nintndo Gamecube"
        ]),
        ["nintendo ds"] =
            new RaSystemInfo(18, ["nintendo ds", "nintendo-ds", "nintendods", "nds", "ds", "nintendo ds"]),
        ["virtual boy"] = new RaSystemInfo(28,
            ["virtual boy", "virtualboy", "vb", "nintendo virtual boy", "nintendo virtualboy", "nintendo vb"]),
        ["pokemon mini"] = new RaSystemInfo(24, ["pokemon mini", "pokémon mini"]),
        ["nintendo dsi"] = new RaSystemInfo(78,
            ["nintendo dsi", "nintendo ds-i", "ndsi", "dsi", "nintendo ndsi", "nintendo dsi"]),
        ["famicom disk system"] = new RaSystemInfo(81,
        [
            "famicom disk system", "fds", "nintendo famicom disk system", "nintendo fds", "Nintendo Family Computer",
            "Nintendo Family Computer Disk System", "Nintendo - Family Computer", "Nintendo Famicom Disc System", "Nintendo FC"
        ]),
        ["wii"] = new RaSystemInfo(19, ["wii", "nintendo wii", "Nintendo Wiiware", "Wiiware"]),
        ["wii u"] = new RaSystemInfo(20,
            ["wii u", "wiiu", "wii-u", "nintendo wii u", "nintendo wiiu", "nintendo wii-u"]),
        ["nintendo 3ds"] = new RaSystemInfo(62,
            ["nintendo 3ds", "3ds", "nintendo3ds", "nintendo 3ds", "Nintendo - Nintendo 3DS"]),

        // Sega systems
        ["genesis/mega drive"] = new RaSystemInfo(1,
        [
            "genesis/mega drive", "genesis", "mega drive", "megadrive", "sega genesis", "sega megadrive",
            "sega mega drive", "Sega Mega Drive (Sega Genesis)"
        ]),
        ["sega cd"] = new RaSystemInfo(9,
        [
            "sega cd", "segacd", "sega genesis cd", "genesis cd", "sega megadrive cd", "megadrive cd",
            "sega mega drive cd", "mega drive cd", "SEGA MEGA-CD", "Mega CD"
        ]),
        ["32x"] = new RaSystemInfo(10,
        [
            "32x", "sega 32x", "sega genesis 32x", "genesis 32x", "megadrive 32x", "mega drive 32x",
            "sega megadrive 32x", "mega drive 32x", "sega mega drive 32x"
        ]),
        ["master system"] = new RaSystemInfo(11,
            ["master system", "mastersystem", "mark3", "Sega Master System", "Sega - Master System - Mark III"]),
        ["game gear"] = new RaSystemInfo(15,
            ["game gear", "gamegear", "sega game gear", "sega gamegear", "Sega - Game Gear"]),
        ["saturn"] = new RaSystemInfo(39, ["saturn", "sega saturn"]),
        ["dreamcast"] = new RaSystemInfo(40, ["dreamcast", "sega dreamcast", "DREAMCAST NAOMI"]),
        ["naomi"] = new RaSystemInfo(27, ["naomi", "sega naomi"]),
        ["sg-1000"] = new RaSystemInfo(33, ["sg-1000", "sg1000", "sega sg-1000", "sega sg1000"]),
        ["sega pico"] = new RaSystemInfo(68, ["sega pico", "pico"]),

        // Sony systems
        ["playstation"] = new RaSystemInfo(12, [
            "playstation", "ps1", "psx", "playstation 1", "sony playstation 1", "sony playstation", "SONY PlayStation",
            "Sony Play Station",
            "Sony Play Station 1", "Sony - PlayStation", "Sony - PlayStation 1", "Sony PlayStation x1"
        ]),
        ["playstation 2"] = new RaSystemInfo(21,
        [
            "playstation 2", "ps2", "sony playstation 2", "Sony Play Station 2", "Sony - PlayStation 2",
            "Sony Playstation2"
        ]),
        ["playstation portable"] =
            new RaSystemInfo(41, ["playstation portable", "psp", "Sony PSP", "SONY PlayStation Portable"]),

        // NEC systems
        ["pc engine/turbografx-16"] = new RaSystemInfo(8,
        [
            "pc engine/turbografx-16", "pc engine", "pcengine", "pc-engine", "turbografx-16", "turbografx 16",
            "turbografx", "turbografx16", "pce", "tg16", "NEC PC Engine", "NEC TurboGrafx 16"
        ]),
        ["pc engine cd/turbografx-cd"] = new RaSystemInfo(76, [
            "pc engine cd/turbografx-cd", "pc engine cd", "pcengine cd", "pcenginecd", "pcecd", "pce-cd",
            "pc-engine cd", "turbografx-cd", "turbografx cd",
            "nec pc engine cd", "nec pcengine cd", "nec pcenginecd", "nec pcecd", "nec pce-cd", "nec pc-engine cd",
            "nec turbografx-cd", "nec turbografx cd", "NEC TurboGrafx 16 CD"
        ]),
        ["supergrafx"] = new RaSystemInfo(8, ["supergrafx", "sgx", "NEC SuperGrafx"]),

        // Atari systems
        ["atari lynx"] = new RaSystemInfo(13, ["atari lynx", "lynx"]),
        ["atari jaguar"] = new RaSystemInfo(17, ["atari jaguar", "jaguar"]),
        ["atari 2600"] = new RaSystemInfo(25, ["atari 2600", "atari2600", "atari vcs"]),
        ["atari 7800"] = new RaSystemInfo(51, ["atari 7800", "atari7800"]),
        ["atari jaguar cd"] = new RaSystemInfo(77, ["atari jaguar cd", "jaguar cd", "jaguarcd"]),
        ["atari st"] = new RaSystemInfo(36, ["atari st", "atari ste", "atarist", "Atari Falcon"]),

        // Other systems
        ["arcade"] = new RaSystemInfo(27, [
            "arcade", "mame", "m.a.m.e.", "arcade games", "arcade classics", "fliperama",
            "neogeo", "neo geo", "neo-geo", "snk neo geo", "snk neogeo", "ARCADE GAME",
            "ARCADE - Multiple Arcade Machine Emulator", "sega model",
            "sega_Model 2", "@sega_Model 2", "@sega model", "CPS 1", "CPS2", "CPS3", "CPS1"
        ]),
        ["neo geo pocket"] = new RaSystemInfo(14,
        [
            "neo geo pocket", "neo geo pocket color", "neogeo pocket", "neogeo pocket color", "ngp", "ngpc",
            "SNK Neo Geo Pocket Color", "SNK Neo Geo Pocket"
        ]),
        ["magnavox odyssey 2"] =
            new RaSystemInfo(23, ["magnavox odyssey 2", "odyssey 2", "odyssey2", "videopac g7000"]),
        ["msx"] = new RaSystemInfo(29,
        [
            "msx", "msx1", "msx2", "Microsoft MSX", "Microsoft MSX2", "Microsoft MSX 2+", "Microsoft MSX 2",
            "Microsoft MSX Turbo-R"
        ]),
        ["amstrad cpc"] = new RaSystemInfo(37,
            ["amstrad cpc", "cpc", "amstrad", "Amstrad PCW", "Amstrad CPC6128", "Amstrad CPC 6128"]),
        ["apple ii"] = new RaSystemInfo(38, ["apple ii", "apple 2", "apple2", "Apple IIGS"]),
        ["3do interactive multiplayer"] =
            new RaSystemInfo(43, ["3do interactive multiplayer", "3do", "3DO real", "Panasonic 3DO"]),
        ["colecovision"] = new RaSystemInfo(44, ["colecovision"]),
        ["intellivision"] = new RaSystemInfo(45, ["intellivision", "intv", "Mattel Intellivision"]),
        ["vectrex"] = new RaSystemInfo(46, ["vectrex", "GCE Vectrex"]),
        ["pc-fx"] = new RaSystemInfo(49, ["pc-fx", "pcfx"]),
        ["wonderswan"] = new RaSystemInfo(53,
            ["wonderswan", "wonderswan color", "BANDAI WonderSwan", "Bandai WonderSwan Color"]),
        ["neo geo cd"] = new RaSystemInfo(56, ["neo geo cd", "neogeo cd", "neo geo compact disc", "SNK Neo Geo CD"]),
        ["watara supervision"] = new RaSystemInfo(63, ["watara supervision", "supervision"]),
        ["mega duck"] = new RaSystemInfo(69, ["mega duck", "creativision"]),
        ["arduboy"] = new RaSystemInfo(71, ["arduboy"]),
        ["wasm-4"] = new RaSystemInfo(72, ["wasm-4"]),
        ["pc-8000/8800"] = new RaSystemInfo(47, ["pc-8000/8800", "pc-8000", "pc-8800", "pc8000", "pc8800"]),
        ["commodore 64"] = new RaSystemInfo(30, [
            "commodore 64", "c64", "Commodore Max", "C64 - Tapes [My Originals]", "C64 - OneLoad64 Collection",
            "C64 - Tapes [Pytronik Collection]", "C64 - Tapes [My Compilations]", "C64 - Ultimate Tape Archive",
            "C64 - Floppy [Scene Collection]"
        ]),
        ["amiga"] = new RaSystemInfo(35, ["amiga", "commodore amiga"]),
        ["zx spectrum"] = new RaSystemInfo(59, ["zx spectrum", "zxspectrum", "spectrum", "Sinclair ZX Spectrum"]),
        ["fairchild channel f"] = new RaSystemInfo(57, ["fairchild channel f", "channel f"]),
        ["philips cd-i"] = new RaSystemInfo(42, ["philips cd-i", "cd-i"]),
        ["sharp x68000"] = new RaSystemInfo(52, ["sharp x68000", "x68000"]),
        ["sharp x1"] = new RaSystemInfo(64, ["sharp x1", "x1"]),
        ["oric"] = new RaSystemInfo(32, ["oric"]),
        ["thomson to8"] = new RaSystemInfo(66, ["thomson to8", "to8"]),
        ["cassette vision"] = new RaSystemInfo(54, ["cassette vision"]),
        ["super cassette vision"] = new RaSystemInfo(55, ["super cassette vision"]),
        ["uzebox"] = new RaSystemInfo(80, ["uzebox"]),
        ["tic-80"] = new RaSystemInfo(65, ["tic-80"]),
        ["ti-83"] = new RaSystemInfo(79, ["ti-83"]),
        ["nokia n-gage"] = new RaSystemInfo(61, ["nokia n-gage", "n-gage"]),
        ["vic-20"] = new RaSystemInfo(34, ["vic-20", "vic20"]),
        ["zx81"] = new RaSystemInfo(31, ["zx81"]),
        ["pc-6000"] = new RaSystemInfo(67, ["pc-6000", "pc6000"]),
        ["game & watch"] = new RaSystemInfo(60, ["game & watch", "game and watch"]),
        ["elektor tv games computer"] = new RaSystemInfo(75, ["elektor tv games computer"]),
        ["interton vc 4000"] = new RaSystemInfo(74, ["interton vc 4000"]),
        ["arcadia 2001"] = new RaSystemInfo(73, ["arcadia 2001"]),
        ["fm towns"] = new RaSystemInfo(58, ["fm towns"]),
        ["hubs"] = new RaSystemInfo(100, ["hubs"]),
        ["events"] = new RaSystemInfo(101, ["events"]),
        ["standalone"] = new RaSystemInfo(102, ["standalone"]),
        ["unsupported"] = new RaSystemInfo(102, [
            "playstation 3", "ps3", "sony playstation 3", "playstation 4", "ps4", "sony playstation 4",
            "playstation vita", "vita", "sony playstation vita", "xbox 360", "xbox360", "x-box 360",
            "microsoft xbox 360", "Microsoft Xbox 360 XBLA", "xbox one", "xboxone", "x-box one", "microsoft xbox one",
            "xbox series x", "xbox series s", "xbox series x/s", "nintendo switch", "switch", "Nintendo - Switch",
            "sega model 2", "model 2", "sega model 3", "sega_model 3", "model 3", "sega naomi 2", "naomi 2",
            "atomiswave",
            "microsoft windows", "windows", "amstrad gx4000", "gx4000", "scummvm", "scumm vm", "scumm-vm",
            "pinball arcade", "pinballarcade", "Arcade - Raine", "Commander Genius", "Matra Alice32", "DVD",
            "NEC PC-FX", "Nintendo SNES MSU1", "Visual Pinball", "PC Arcade", "Atari 5200", "Super ACan",
            "Mattel Aquarius",
            "LaserDisk", "Adobe Flash", "Emulators", "Bit Corporation Gamate", "Gamate", "Bandai RX-78", "Apple Mac",
            "Apple III", "Acorn Electron", "Acorn Atom", "Acorn Archimedes", "Acorn BBC Micro", "Archimedes",
            "Casio CFX-9850", "Commodore 128", "Casio PV-1000", "Casio PV-2000", "Casio FP-1000", "Sega MSU MD",
            "ra", "FBA2", "FBA", "Bally Astrocade", "unsupported", "Build Engine", "Elektronika BK-001-411",
            "PC-Port", "Nintendo Game and Watch", "Namco 2x6", "FBN", "J2ME", "NEC PC-88", "CGenius", "NEC PC88",
            "Fujitsu FM-7", "Fairchild Channel-F", "Exelvision EX100", "Enterprise 64-128", "Capcom Medalusion",
            "Sharp MZ-700", "Gakken Compact Vision", "Gamepark GP32", "Exidy Sorcerer", "Cybiko", "Mophun",
            "PEL Varazdin Orao", "NEC PC-9801", "NEC PC-8801", "NEC PC-8001", "PC-8801", "NEC PC-6001", "SEGA_RACING",
            "Mitsubishi Multi-8", "Memotech MTX", "Matsushita JR-100", "M&D Monon Color", "Jupiter Ace", "Java",
            "Sord M5", "Sinclair QL", "Vector06c", "Sharp MZ-2200", "Sega Super Control Station", "Sega SC-3000",
            "Sega SC3000", "Sega SC 3000", "Sam Coupé", "RM Nimbus", "RCA Chip-8", "Philips VG5000", "RCA Studio II",
            "Philips P2000", "SEGA_action", "ReflectionHLE", "Spectravideo", "Symbian", "Tandy TRS80",
            "Tangerine Microtan 65",
            "Tatung Einstein TC-01", "SEGA_fighting", "SEGA_arcade", "SEGA_SPORTS", "Metroid Saga", "Sega Chihiro",
            "ReflectionHLE",
            "Technosys Aamber Pegasus", "TI-99", "TI-73", "TI-80", "@sega_model 3", "sega model 3m", "FBNeo",
            "EACA Colour Genie",
            "NEC PC9801", "NEC PC9821", "Metroid Saga", "PC_Shooter_1998-2002", "super a'can", "NEC PC98", "MAMEICHON",
            "Videoton TVC",
            "Tiger GameCom", "Tiger Game.com", "Game.com", "Systema1", "Tomy Pyuta",
            "C64 - Cartridge [EasyFlash Collection]",
            "Luxor ABC 80", "90er_PC_Spiele_und_die_frühen_2000", "TestDOS"
        ]),
        ["Xbox"] = new RaSystemInfo(22, ["xbox", "x-box", "Microsoft Xbox"]),
        ["DOS"] = new RaSystemInfo(26, ["dos", "microsoft dos", "MS DOS", "MSDOS", "MS-DOS"]),
        ["PC-9800"] = new RaSystemInfo(48, ["pc-9800", "pc9800"]),
        ["Zeebo"] = new RaSystemInfo(70, ["Zeebo"])
    };

    // Systems whose hashing logic is not implemented by the bundled RetroAchievementsSharp
    // CLI (a 1:1 rcheevos port). Matching is exact and separator-insensitive: substring
    // matching would wrongly reject supported systems (e.g. "Arcade" vs "SEGA_arcade").
    private static readonly string[] UnsupportedHashLogicSystems =
    [
        "sega pico", "xbox", "xbox360",
        "atari st", "commodore 64", "amiga", "zx spectrum",
        "philips cd-i", "sharp x68000", "sharp x1", "oric", "thomson to8", "cassette vision",
        "super cassette vision", "uzebox", "tic-80", "ti-83", "nokia n-gage", "vic-20", "zx81",
        "pc-6000", "game & watch", "elektor tv games computer", "interton vc 4000",
        "arcadia 2001", "fm towns", "hubs", "events", "standalone", "atari 800", "microsoft windows",
        "atari 5200", "atari 8-bit"
    ];

    private readonly HashSet<string> _loggedUnmatchedSystems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsSystemMatcher" /> class.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public RetroAchievementsSystemMatcher(ILogger logErrors, ILogger logger)
    {
        _logger = logErrors;
        _logger = logger;
    }

    /// <summary>
    ///     Finds the best matching RetroAchievements system name using fuzzy matching.
    /// </summary>
    /// <param name="inputSystemName">The system name to match</param>
    /// <returns>The normalized RetroAchievements system name, or the original if no match found</returns>
    public string GetBestMatchSystemName(string inputSystemName)
    {
        if (string.IsNullOrWhiteSpace(inputSystemName))
            return inputSystemName;

        var normalizedInput = inputSystemName.Trim().ToLowerInvariant();

        // Direct exact match (faster for common cases)
        foreach (var kvp in SystemMappings)
        {
            if (kvp.Value.Aliases.Any(pattern => pattern.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase)))
                return kvp.Key;
        }

        // No match found, log it once per unique system name for future improvement
        if (_loggedUnmatchedSystems.Add(inputSystemName))
        {
            _logger?.Debug(
                $"[RA System Matcher] No match found for system name: '{inputSystemName}'. Consider adding it as an alias.");
            _logger?.Warning(
                $"[RA System Matcher] No match found for system name: '{inputSystemName}'. Consider adding it as an alias.");
        }

        return normalizedInput;
    }

    /// <summary>
    ///     Checks whether the given system name is an official RetroAchievements system name (a key in the SystemMappings
    ///     dictionary).
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the system name is an official key; otherwise, false.</returns>
    public bool IsOfficialSystemName(string systemName)
    {
        return SystemMappings.ContainsKey(systemName.ToLowerInvariant());
    }

    /// <summary>
    ///     Gets a sorted list of all official RetroAchievements system names supported by the matcher.
    /// </summary>
    /// <returns>A sorted list of system name strings.</returns>
    public IList<string> GetSupportedSystemNames()
    {
        return SystemMappings.Keys.OrderBy(static s => s, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    ///     Gets the RetroAchievements Console ID for a given system name.
    /// </summary>
    /// <param name="inputSystemName">The system name to look up.</param>
    /// <returns>The console ID, or -1 if not found.</returns>
    public int GetSystemId(string inputSystemName)
    {
        _logger.Debug($"[GetSystemId] Looking up system ID for '{inputSystemName}'");
        var bestMatch = GetBestMatchSystemName(inputSystemName);
        _logger.Debug($"[GetSystemId] Best match: '{bestMatch}'");
        return SystemMappings.TryGetValue(bestMatch, out var systemInfo) ? systemInfo.Id : -1;
    }

    /// <summary>
    ///     Attempts to find an exact match for the input system name among all known aliases.
    /// </summary>
    /// <param name="inputSystemName">The system name to match.</param>
    /// <returns>The official system name key if an exact alias match is found; otherwise, null.</returns>
    public string? GetExactAliasMatch(string inputSystemName)
    {
        if (string.IsNullOrWhiteSpace(inputSystemName)) return null;

        var normalizedInput = inputSystemName.Trim().ToLowerInvariant();

        foreach (var kvp in SystemMappings)
        {
            if (kvp.Value.Aliases.Any(pattern => pattern.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase)))
                return kvp.Key; // Found 100% match
        }

        return null; // No exact match
    }

    /// <summary>
    ///     Checks if a system name exists in the SystemMappings dictionary.
    ///     This checks both the dictionary keys and all aliases for each system.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the system exists in SystemMappings; otherwise, false.</returns>
    public bool IsSystemInMappings(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            return false;

        var normalizedInput = systemName.Trim().ToLowerInvariant();

        // Check if input matches any dictionary key directly
        if (SystemMappings.ContainsKey(normalizedInput))
            return true;

        // Check if input matches any alias in any system
        foreach (var kvp in SystemMappings)
        {
            if (kvp.Value.Aliases.Any(alias =>
                    alias.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase) ||
                    alias.Contains(normalizedInput, StringComparison.OrdinalIgnoreCase) ||
                    normalizedInput.Contains(alias, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Try fuzzy matching as a last resort
        var bestMatch = GetBestMatchSystemName(systemName);
        return SystemMappings.ContainsKey(bestMatch);
    }

    /// <summary>
    ///     Determines whether the system can be hashed for RetroAchievements by the bundled
    ///     CLI tool: its resolved console has a usable id and its hash logic is known.
    ///     This is the single source of truth for both the hasher tool UI and the
    ///     background hash scanner.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <returns>True if the system is supported for RetroAchievements hashing; otherwise, false.</returns>
    public bool IsSystemSupportedForHashing(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            return false;

        var matchedName = GetExactAliasMatch(systemName) ?? GetBestMatchSystemName(systemName);

        if (IsUnsupportedHashLogicSystem(matchedName)) return false;

        return GetSystemId(matchedName) is > 0 and <= RetroAchievementsConstants.MaxConsoleId;
    }

    /// <summary>
    ///     Checks the unsupported list with an exact, separator-insensitive match.
    ///     Substring matching must not be used here: "Arcade" is supported while
    ///     "SEGA_arcade" is not.
    /// </summary>
    private static bool IsUnsupportedHashLogicSystem(string systemName)
    {
        var normalized = NormalizeForHashLogicMatch(systemName);

        foreach (var unsupported in UnsupportedHashLogicSystems)
        {
            if (normalized.Equals(NormalizeForHashLogicMatch(unsupported), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string NormalizeForHashLogicMatch(string systemName)
    {
        return systemName.Trim().ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace("/", string.Empty)
            .Replace("&", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .Replace("'", string.Empty)
            .Replace("_", string.Empty)
            .Replace("+", string.Empty)
            .Replace("™", string.Empty)
            .Replace("®", string.Empty);
    }

    /// <summary>
    ///     Holds information about a RetroAchievements system, including its ID and name aliases.
    /// </summary>
    public class RaSystemInfo
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RaSystemInfo" /> class.
        /// </summary>
        /// <param name="id">The official RetroAchievements console ID.</param>
        /// <param name="aliases">The name aliases used for matching the system.</param>
        public RaSystemInfo(int id, string[] aliases)
        {
            Id = id;
            Aliases = aliases;
        }

        /// <summary>
        ///     Gets the official RetroAchievements console ID for the system.
        /// </summary>
        public int Id { get; }

        /// <summary>
        ///     Gets the list of known name aliases for the system used for matching.
        /// </summary>
        public string[] Aliases { get; }
    }
}