using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.HelpUser;
using SimpleLauncher.Interfaces;

namespace SimpleLauncher.Services.HelpUser;

/// <summary>
///     Provides emulator parameter help text for systems and renders it in a RichTextBox.
/// </summary>
public partial class HelpUserService : IHelpUserService
{
    private readonly ILogger _logger;
    private readonly HelpUserManager _manager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HelpUserService" /> class and starts loading the parameters file.
    /// </summary>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="messageBoxLibrary">The message box service for user notifications.</param>
    public HelpUserService(ILogger logErrors, IMessageBoxLibraryService messageBoxLibrary)
    {
        _logger = logErrors;
        _manager = new HelpUserManager(logErrors, messageBoxLibrary);
        try
        {
            _ = _manager.LoadAsync(); // Load parameters.md
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Failed to load parameters.md.";
            _logger.Error(ex, contextMessage);
        }
    }

    /// <summary>
    ///     Gets the help text for the given system name, falling back to a message
    ///     when no information is available for the system.
    /// </summary>
    /// <param name="systemName">The name of the system to get help for.</param>
    /// <returns>The help text for the system.</returns>
    public string GetHelpText(string systemName)
    {
        if (string.IsNullOrEmpty(systemName))
            return (string)Application.Current.TryFindResource("Nosystemnameprovided") ?? "No system name provided.";

        var responses = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Amstrad CPC", AmstradCpcDetails },
            { "CPC", AmstradCpcDetails },
            { "Amstrad CPC GX4000", AmstradCpcgx4000Details },
            { "Amstrad GX4000", AmstradCpcgx4000Details },
            { "CPC GX4000", AmstradCpcgx4000Details },
            { "GX4000", AmstradCpcgx4000Details },
            { "Arcade", ArcadeDetails },
            { "Mame", ArcadeDetails },
            { "Raine", ArcadeDetails },
            { "Atari 2600", Atari2600Details },
            { "Atari2600", Atari2600Details },
            { "Atari 5200", Atari5200Details },
            { "Atari5200", Atari5200Details },
            { "Atari 7800", Atari7800Details },
            { "Atari7800", Atari7800Details },
            { "Atari 8-Bit", Atari8BitDetails },
            { "Atari 8-Bits", Atari8BitDetails },
            { "Atari 8 bits", Atari8BitDetails },
            { "Atari 8bits", Atari8BitDetails },
            { "Atari 800", Atari8BitDetails },
            { "Atari Jaguar", AtariJaguarDetails },
            { "Jaguar", AtariJaguarDetails },
            { "Atari Jaguar CD", AtariJaguarCdDetails },
            { "Jaguar CD", AtariJaguarCdDetails },
            { "Atari Lynx", AtariLynxDetails },
            { "Lynx", AtariLynxDetails },
            { "Atari ST", AtariStDetails },
            { "AtariST", AtariStDetails },
            { "Atomiswave", AtomiswaveDetails },
            { "Bandai WonderSwan", BandaiWonderSwanDetails },
            { "Bandai Wonder Swan", BandaiWonderSwanDetails },
            { "WonderSwan", BandaiWonderSwanDetails },
            { "Wonder Swan", BandaiWonderSwanDetails },
            { "Bandai WonderSwan Color", BandaiWonderSwanColorDetails },
            { "Bandai Wonder Swan Color", BandaiWonderSwanColorDetails },
            { "WonderSwan Color", BandaiWonderSwanColorDetails },
            { "Wonder Swan Color", BandaiWonderSwanColorDetails },
            { "Casio PV-1000", CasioPv1000Details },
            { "Casio PV1000", CasioPv1000Details },
            { "Casio PV 1000", CasioPv1000Details },
            { "Commander Genius", CommanderGeniusDetails },
            { "Comander Genius", CommanderGeniusDetails },
            { "PV-1000", CasioPv1000Details },
            { "PV1000", CasioPv1000Details },
            { "PV 1000", CasioPv1000Details },
            { "Colecovision", ColecovisionDetails },
            { "Commodore 64", Commodore64Details },
            { "Commodore64", Commodore64Details },
            { "Commodore 128", Commodore128Details },
            { "Commodore128", Commodore128Details },
            { "Amiga", CommodoreAmigaDetails },
            { "Commodore Amiga", CommodoreAmigaDetails },
            { "Commodore Amiga CD32", CommodoreAmigaCd32Details },
            { "Commodore Amiga CD", CommodoreAmigaCd32Details },
            { "Amiga CD", CommodoreAmigaCd32Details },
            { "Amiga CD32", CommodoreAmigaCd32Details },
            { "AmigaCD", CommodoreAmigaCd32Details },
            { "AmigaCD32", CommodoreAmigaCd32Details },
            { "FMTowns", FmTownsDetails },
            { "FM Towns", FmTownsDetails },
            { "LaserDisk", LaserDiskDetails },
            { "Laser Disk", LaserDiskDetails },
            { "Daphne", LaserDiskDetails },
            { "Magnavox Odyssey 2", MagnavoxOdyssey2Details },
            { "Odyssey", MagnavoxOdyssey2Details },
            { "Mattel Aquarius", MattelAquariusDetails },
            { "Aquarius", MattelAquariusDetails },
            { "Mattel Intellivision", MattelIntellivisionDetails },
            { "Intellivision", MattelIntellivisionDetails },
            { "Microsoft DOS", MicrosoftDosDetails },
            { "DOS", MicrosoftDosDetails },
            { "Microsoft MSX", MicrosoftMsxDetails },
            { "MSX", MicrosoftMsxDetails },
            { "MSX1", MicrosoftMsxDetails },
            { "Microsoft MSX2", MicrosoftMsx2Details },
            { "MSX2", MicrosoftMsx2Details },
            { "MSX 2", MicrosoftMsx2Details },
            { "Microsoft Windows", MicrosoftWindowsDetails },
            { "Windows", MicrosoftWindowsDetails },
            { "PC", MicrosoftWindowsDetails },
            { "Microsoft Xbox", MicrosoftXboxDetails },
            { "Xbox", MicrosoftXboxDetails },
            { "Xbox Original", MicrosoftXboxDetails },
            { "Microsoft Xbox 360", MicrosoftXbox360Details },
            { "Xbox 360", MicrosoftXbox360Details },
            { "Xbox360", MicrosoftXbox360Details },
            { "Microsoft Xbox 360 XBLA", MicrosoftXbox360XblaDetails },
            { "Xbox 360 XBLA", MicrosoftXbox360XblaDetails },
            { "Xbox360 XBLA", MicrosoftXbox360XblaDetails },
            { "XBLA", MicrosoftXbox360XblaDetails },
            { "NEC PC Engine", NecpcEngineDetails },
            { "PC Engine", NecpcEngineDetails },
            { "PCEngine", NecpcEngineDetails },
            { "NEC PC Engine CD", NecpcEngineCdDetails },
            { "PC Engine CD", NecpcEngineCdDetails },
            { "PCEngine CD", NecpcEngineCdDetails },
            { "PCEngineCD", NecpcEngineCdDetails },
            { "NEC PC-FX", NecpcfxDetails },
            { "PC-FX", NecpcfxDetails },
            { "PCFX", NecpcfxDetails },
            { "NEC SuperGrafx", NecSuperGrafxDetails },
            { "SuperGrafx", NecSuperGrafxDetails },
            { "NEC TurboGrafx-16", NecTurboGrafx16Details },
            { "NEC TurboGrafx 16", NecTurboGrafx16Details },
            { "NEC TurboGrafx", NecTurboGrafx16Details },
            { "TurboGrafx", NecTurboGrafx16Details },
            { "TurboGrafx16", NecTurboGrafx16Details },
            { "Nintendo 3DS", Nintendo3DsDetails },
            { "Nintendo3DS", Nintendo3DsDetails },
            { "3DS", Nintendo3DsDetails },
            { "Nintendo 64", Nintendo64Details },
            { "Nintendo64", Nintendo64Details },
            { "N64", Nintendo64Details },
            { "Nintendo 64DD", Nintendo64DdDetails },
            { "Nintendo64DD", Nintendo64DdDetails },
            { "N64DD", Nintendo64DdDetails },
            { "Nintendo DS", NintendoDsDetails },
            { "NintendoDS", NintendoDsDetails },
            { "DS", NintendoDsDetails },
            { "Nintendo Family Computer Disk System", NintendoFamilyComputerDiskSystemDetails },
            { "Family Computer Disk System", NintendoFamilyComputerDiskSystemDetails },
            { "Nintendo Game Boy", NintendoGameBoyDetails },
            { "Game Boy", NintendoGameBoyDetails },
            { "GameBoy", NintendoGameBoyDetails },
            { "Nintendo Game Boy Advance", NintendoGameBoyAdvanceDetails },
            { "Game Boy Advance", NintendoGameBoyAdvanceDetails },
            { "GameBoy Advance", NintendoGameBoyAdvanceDetails },
            { "Nintendo Game Boy Color", NintendoGameBoyColorDetails },
            { "Game Boy Color", NintendoGameBoyColorDetails },
            { "GameBoy Color", NintendoGameBoyColorDetails },
            { "Nintendo GameCube", NintendoGameCubeDetails },
            { "GameCube", NintendoGameCubeDetails },
            { "Nintendo NES", NintendoNesDetails },
            { "NES", NintendoNesDetails },
            { "Nintendo Entertainment System", NintendoNesDetails },
            { "Nintendo Famicom", NintendoNesDetails },
            { "Famicom", NintendoNesDetails },
            { "Nintendo Satellaview", NintendoSatellaviewDetails },
            { "Satellaview", NintendoSatellaviewDetails },
            { "Nintendo SNES", NintendoSnesDetails },
            { "SNES", NintendoSnesDetails },
            { "Super Nintendo", NintendoSnesDetails },
            { "Super NES", NintendoSnesDetails },
            { "Nintendo Super Famicom", NintendoSnesDetails },
            { "Super Famicom", NintendoSnesDetails },
            { "Nintendo SNES MSU1", NintendoSnesmsu1Details },
            { "Nintendo Super NES MSU1", NintendoSnesmsu1Details },
            { "SNES MSU1", NintendoSnesmsu1Details },
            { "MSU1", NintendoSnesmsu1Details },
            { "Super NES MSU1", NintendoSnesmsu1Details },
            { "Nintendo Switch", NintendoSwitchDetails },
            { "Switch", NintendoSwitchDetails },
            { "Nintendo Virtual Boy", NintendoVirtualBoyDetails },
            { "Nintendo VirtualBoy", NintendoVirtualBoyDetails },
            { "Virtual Boy", NintendoVirtualBoyDetails },
            { "VirtualBoy", NintendoVirtualBoyDetails },
            { "Virtual-Boy", NintendoVirtualBoyDetails },
            { "V-Boy", NintendoVirtualBoyDetails },
            { "VBoy", NintendoVirtualBoyDetails },
            { "Nintendo Wii", NintendoWiiDetails },
            { "Wii", NintendoWiiDetails },
            { "Nintendo WiiU", NintendoWiiUDetails },
            { "WiiU", NintendoWiiUDetails },
            { "Nintendo WiiWare", NintendoWiiWareDetails },
            { "WiiWare", NintendoWiiWareDetails },
            { "Panasonic 3DO", Panasonic3DoDetails },
            { "Panasonic3DO", Panasonic3DoDetails },
            { "3DO", Panasonic3DoDetails },
            { "Philips CD-i", PhilipsCDiDetails },
            { "Philips CDi", PhilipsCDiDetails },
            { "CD-i", PhilipsCDiDetails },
            { "CDi", PhilipsCDiDetails },
            { "ScummVM", ScummVmDetails },
            { "Scumm-VM", ScummVmDetails },
            { "Sega Dreamcast", SegaDreamcastDetails },
            { "Dreamcast", SegaDreamcastDetails },
            { "Sega Game Gear", SegaGameGearDetails },
            { "Game Gear", SegaGameGearDetails },
            { "GameGear", SegaGameGearDetails },
            { "Sega Genesis", SegaGenesisDetails },
            { "Genesis", SegaGenesisDetails },
            { "Mega Drive", SegaGenesisDetails },
            { "MegaDrive", SegaGenesisDetails },
            { "Sega Genesis 32X", SegaGenesis32XDetails },
            { "Genesis 32X", SegaGenesis32XDetails },
            { "Genesis32X", SegaGenesis32XDetails },
            { "Sega 32X", SegaGenesis32XDetails },
            { "Sega32X", SegaGenesis32XDetails },
            { "Sega Genesis CD", SegaGenesisCdDetails },
            { "Genesis CD", SegaGenesisCdDetails },
            { "GenesisCD", SegaGenesisCdDetails },
            { "Sega Master System", SegaMasterSystemDetails },
            { "MasterSystem", SegaMasterSystemDetails },
            { "Master System", SegaMasterSystemDetails },
            { "Sega Mark3", SegaMasterSystemDetails },
            { "Mark3", SegaMasterSystemDetails },
            { "Sega MarkIII", SegaMasterSystemDetails },
            { "MarkIII", SegaMasterSystemDetails },
            { "Mark III", SegaMasterSystemDetails },
            { "Sega Model 3", SegaModel3Details },
            { "Model 3", SegaModel3Details },
            { "Model3", SegaModel3Details },
            { "Sega Naomi", SegaNaomiDetails },
            { "SegaNaomi", SegaNaomiDetails },
            { "Naomi", SegaNaomiDetails },
            { "Sega Naomi2", SegaNaomi2Details },
            { "SegaNaomi2", SegaNaomi2Details },
            { "Naomi2", SegaNaomi2Details },
            { "Sega Saturn", SegaSaturnDetails },
            { "Saturn", SegaSaturnDetails },
            { "Sega SC-3000", SegaSc3000Details },
            { "Sega SC3000", SegaSc3000Details },
            { "SC-3000", SegaSc3000Details },
            { "SC3000", SegaSc3000Details },
            { "Sega SG-1000", SegaSg1000Details },
            { "Sega SG1000", SegaSg1000Details },
            { "SG-1000", SegaSg1000Details },
            { "SG1000", SegaSg1000Details },
            { "Sharp x68000", Sharpx68000Details },
            { "Sharp x-68000", Sharpx68000Details },
            { "x68000", Sharpx68000Details },
            { "x-68000", Sharpx68000Details },
            { "Sinclair ZX Spectrum", SinclairZxSpectrumDetails },
            { "ZX Spectrum", SinclairZxSpectrumDetails },
            { "ZX-Spectrum", SinclairZxSpectrumDetails },
            { "Spectrum", SinclairZxSpectrumDetails },
            { "SNK Neo Geo", SnkNeoGeoDetails },
            { "SNK NeoGeo", SnkNeoGeoDetails },
            { "Neo Geo", SnkNeoGeoDetails },
            { "NeoGeo", SnkNeoGeoDetails },
            { "SNK Neo Geo CD", SnkNeoGeoCdDetails },
            { "SNK NeoGeo CD", SnkNeoGeoCdDetails },
            { "SNK NeoGeoCD", SnkNeoGeoCdDetails },
            { "Neo Geo CD", SnkNeoGeoCdDetails },
            { "NeoGeo CD", SnkNeoGeoCdDetails },
            { "NeoGeoCD", SnkNeoGeoCdDetails },
            { "SNK Neo Geo Pocket", SnkNeoGeoPocketDetails },
            { "SNK NeoGeo Pocket", SnkNeoGeoPocketDetails },
            { "NeoGeo Pocket", SnkNeoGeoPocketDetails },
            { "SNK Neo Geo Pocket Color", SnkNeoGeoPocketColorDetails },
            { "SNK NeoGeo Pocket Color", SnkNeoGeoPocketColorDetails },
            { "Neo Geo Pocket Color", SnkNeoGeoPocketColorDetails },
            { "NeoGeo Pocket Color", SnkNeoGeoPocketColorDetails },
            { "Sony PlayStation 1", SonyPlayStation1Details },
            { "PlayStation 1", SonyPlayStation1Details },
            { "PlayStation", SonyPlayStation1Details },
            { "PSX", SonyPlayStation1Details },
            { "PSX1", SonyPlayStation1Details },
            { "PSX 1", SonyPlayStation1Details },
            { "Sony PlayStation 2", SonyPlayStation2Details },
            { "PlayStation 2", SonyPlayStation2Details },
            { "PSX2", SonyPlayStation2Details },
            { "PSX 2", SonyPlayStation2Details },
            { "Sony PlayStation 3", SonyPlayStation3Details },
            { "PlayStation 3", SonyPlayStation3Details },
            { "PSX3", SonyPlayStation3Details },
            { "PSX 3", SonyPlayStation3Details },
            { "Sony PlayStation 4", SonyPlayStation4Details },
            { "PlayStation 4", SonyPlayStation4Details },
            { "PSX4", SonyPlayStation4Details },
            { "PSX 4", SonyPlayStation4Details },
            { "Sony PlayStation Vita", SonyPlayStationVitaDetails },
            { "PlayStation Vita", SonyPlayStationVitaDetails },
            { "Vita", SonyPlayStationVitaDetails },
            { "Sony PSP", SonyPspDetails },
            { "PlayStation Portable", SonyPspDetails },
            { "PSP", SonyPspDetails },
            { "Super A'Can", SuperAcanDetails },
            { "Super ACan", SuperAcanDetails },
            { "Super-A'Can", SuperAcanDetails },
            { "Super-ACan", SuperAcanDetails },
            { "SuperA'Can", SuperAcanDetails },
            { "SuperACan", SuperAcanDetails },
            { "A'Can", SuperAcanDetails },
            { "ACan", SuperAcanDetails },
            { "Zeebo", ZeeboDetails }
        };

        // Check if a response exists for the given system name
        if (responses.TryGetValue(systemName, out var responseGenerator)) return responseGenerator();

        var noinformationavailableforsystem2 =
            (string)Application.Current.TryFindResource("Noinformationavailableforsystem") ??
            "No information available for system:";
        return $"{noinformationavailableforsystem2} {systemName}";
    }

    /// <summary>
    ///     Updates the content of a RichTextBox with formatted text for the given system name.
    /// </summary>
    public void UpdateHelpUserTextBlock(RichTextBox helpUserRichTextBox, string systemName)
    {
        helpUserRichTextBox.Document.Blocks.Clear();
        var text = GetHelpText(systemName);
        SetTextWithMarkdownInternal(helpUserRichTextBox, text);
    }

    // Renamed for clarity: Matches **bold text**
    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the bold text")]
    [GeneratedRegex(@"\*\*(.*?)\*\*", RegexOptions.Compiled, 1000)]
    private static partial Regex BoldRegex();

    // Renamed for clarity: Matches ## headings
    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the heading text")]
    [GeneratedRegex(@"^##\s*(.*?)$", RegexOptions.Multiline, 1000)]
    private static partial Regex HeadingRegex();

    // New regex for Markdown links: [text](url)
    [GeneratedRegex(@"\[(?<text>[^\]]+?)\]\((?<url>https?://\S+?)\)", RegexOptions.Compiled, 1000)]
    private static partial Regex MarkdownLinkRegex();

    // Renamed for clarity: Matches raw URLs like http://example.com or www.example.com
    [GeneratedRegex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.Compiled, 1000)]
    private static partial Regex RawUrlRegex();

    private string AmstradCpcDetails()
    {
        return GetSystemDetails("Amstrad CPC");
    }

    private string AmstradCpcgx4000Details()
    {
        return GetSystemDetails("Amstrad GX4000");
    }

    private string ArcadeDetails()
    {
        return GetSystemDetails("Arcade");
    }

    private string Atari2600Details()
    {
        return GetSystemDetails("Atari 2600");
    }

    private string Atari5200Details()
    {
        return GetSystemDetails("Atari 5200");
    }

    private string Atari7800Details()
    {
        return GetSystemDetails("Atari 7800");
    }

    private string Atari8BitDetails()
    {
        return GetSystemDetails("Atari 8-Bit");
    }

    private string AtariJaguarDetails()
    {
        return GetSystemDetails("Atari Jaguar");
    }

    private string AtariJaguarCdDetails()
    {
        return GetSystemDetails("Atari Jaguar CD");
    }

    private string AtariLynxDetails()
    {
        return GetSystemDetails("Atari Lynx");
    }

    private string AtariStDetails()
    {
        return GetSystemDetails("Atari ST");
    }

    private string AtomiswaveDetails()
    {
        return GetSystemDetails("Atomiswave");
    }

    private string BandaiWonderSwanDetails()
    {
        return GetSystemDetails("Bandai WonderSwan");
    }

    private string BandaiWonderSwanColorDetails()
    {
        return GetSystemDetails("Bandai WonderSwan Color");
    }

    private string CasioPv1000Details()
    {
        return GetSystemDetails("Casio PV-1000");
    }

    private string ColecovisionDetails()
    {
        return GetSystemDetails("Colecovision");
    }

    private string CommanderGeniusDetails()
    {
        return GetSystemDetails("Commander Genius");
    }

    private string Commodore64Details()
    {
        return GetSystemDetails("Commodore 64");
    }

    private string Commodore128Details()
    {
        return GetSystemDetails("Commodore 128");
    }

    private string CommodoreAmigaDetails()
    {
        return GetSystemDetails("Commodore Amiga");
    }

    private string CommodoreAmigaCd32Details()
    {
        return GetSystemDetails("Commodore Amiga CD32");
    }

    private string FmTownsDetails()
    {
        return GetSystemDetails("FM Towns");
    }

    private string LaserDiskDetails()
    {
        return GetSystemDetails("LaserDisk");
    }

    private string MagnavoxOdyssey2Details()
    {
        return GetSystemDetails("Magnavox Odyssey 2");
    }

    private string MattelAquariusDetails()
    {
        return GetSystemDetails("Mattel Aquarius");
    }

    private string MattelIntellivisionDetails()
    {
        return GetSystemDetails("Mattel Intellivision");
    }

    private string MicrosoftDosDetails()
    {
        return GetSystemDetails("Microsoft DOS");
    }

    private string MicrosoftMsxDetails()
    {
        return GetSystemDetails("Microsoft MSX");
    }

    private string MicrosoftMsx2Details()
    {
        return GetSystemDetails("Microsoft MSX2");
    }

    private string MicrosoftWindowsDetails()
    {
        return GetSystemDetails("Microsoft Windows");
    }

    private string MicrosoftXboxDetails()
    {
        return GetSystemDetails("Microsoft Xbox");
    }

    private string MicrosoftXbox360Details()
    {
        return GetSystemDetails("Microsoft Xbox 360");
    }

    private string MicrosoftXbox360XblaDetails()
    {
        return GetSystemDetails("Microsoft Xbox 360 XBLA");
    }

    private string NecpcEngineDetails()
    {
        return GetSystemDetails("NEC PC Engine");
    }

    private string NecTurboGrafx16Details()
    {
        return GetSystemDetails("NEC TurboGrafx-16");
    }

    private string NecpcEngineCdDetails()
    {
        return GetSystemDetails("NEC PC Engine CD");
    }

    private string NecpcfxDetails()
    {
        return GetSystemDetails("NEC PC-FX");
    }

    private string NecSuperGrafxDetails()
    {
        return GetSystemDetails("NEC SuperGrafx");
    }

    private string Nintendo3DsDetails()
    {
        return GetSystemDetails("Nintendo 3DS");
    }

    private string Nintendo64Details()
    {
        return GetSystemDetails("Nintendo 64");
    }

    private string Nintendo64DdDetails()
    {
        return GetSystemDetails("Nintendo 64DD");
    }

    private string NintendoDsDetails()
    {
        return GetSystemDetails("Nintendo DS");
    }

    private string NintendoFamilyComputerDiskSystemDetails()
    {
        return GetSystemDetails("Nintendo Family Computer Disk System");
    }

    private string NintendoGameBoyDetails()
    {
        return GetSystemDetails("Nintendo Game Boy");
    }

    private string NintendoGameBoyAdvanceDetails()
    {
        return GetSystemDetails("Nintendo Game Boy Advance");
    }

    private string NintendoGameBoyColorDetails()
    {
        return GetSystemDetails("Nintendo Game Boy Color");
    }

    private string NintendoGameCubeDetails()
    {
        return GetSystemDetails("Nintendo GameCube");
    }

    private string NintendoNesDetails()
    {
        return GetSystemDetails("Nintendo NES");
    }

    private string NintendoSatellaviewDetails()
    {
        return GetSystemDetails("Nintendo Satellaview");
    }

    private string NintendoSnesDetails()
    {
        return GetSystemDetails("Nintendo SNES");
    }

    private string NintendoSnesmsu1Details()
    {
        return GetSystemDetails("Nintendo SNES MSU1");
    }

    private string NintendoSwitchDetails()
    {
        return GetSystemDetails("Nintendo Switch");
    }

    private string NintendoWiiDetails()
    {
        return GetSystemDetails("Nintendo Wii");
    }

    private string NintendoWiiUDetails()
    {
        return GetSystemDetails("Nintendo WiiU");
    }

    private string NintendoWiiWareDetails()
    {
        return GetSystemDetails("Nintendo WiiWare");
    }

    private string NintendoVirtualBoyDetails()
    {
        return GetSystemDetails("Nintendo Virtual Boy");
    }

    private string Panasonic3DoDetails()
    {
        return GetSystemDetails("Panasonic 3DO");
    }

    private string PhilipsCDiDetails()
    {
        return GetSystemDetails("Philips CD-i");
    }

    private string ScummVmDetails()
    {
        return GetSystemDetails("ScummVM");
    }

    private string SegaDreamcastDetails()
    {
        return GetSystemDetails("Sega Dreamcast");
    }

    private string SegaGameGearDetails()
    {
        return GetSystemDetails("Sega Game Gear");
    }

    private string SegaGenesisDetails()
    {
        return GetSystemDetails("Sega Genesis");
    }

    private string SegaGenesis32XDetails()
    {
        return GetSystemDetails("Sega Genesis 32X");
    }

    private string SegaGenesisCdDetails()
    {
        return GetSystemDetails("Sega Genesis CD");
    }

    private string SegaMasterSystemDetails()
    {
        return GetSystemDetails("Sega Master System");
    }

    private string SegaModel3Details()
    {
        return GetSystemDetails("Sega Model 3");
    }

    private string SegaNaomiDetails()
    {
        return GetSystemDetails("Sega Naomi");
    }

    private string SegaNaomi2Details()
    {
        return GetSystemDetails("Sega Naomi 2");
    }

    private string SegaSaturnDetails()
    {
        return GetSystemDetails("Sega Saturn");
    }

    private string SegaSc3000Details()
    {
        return GetSystemDetails("Sega SC-3000");
    }

    private string SegaSg1000Details()
    {
        return GetSystemDetails("Sega SG-1000");
    }

    private string Sharpx68000Details()
    {
        return GetSystemDetails("Sharp x68000");
    }

    private string SinclairZxSpectrumDetails()
    {
        return GetSystemDetails("Sinclair ZX Spectrum");
    }

    private string SnkNeoGeoDetails()
    {
        return GetSystemDetails("SNK Neo Geo");
    }

    private string SnkNeoGeoCdDetails()
    {
        return GetSystemDetails("SNK Neo Geo CD");
    }

    private string SnkNeoGeoPocketDetails()
    {
        return GetSystemDetails("SNK Neo Geo Pocket");
    }

    private string SnkNeoGeoPocketColorDetails()
    {
        return GetSystemDetails("SNK Neo Geo Pocket Color");
    }

    private string SonyPlayStation1Details()
    {
        return GetSystemDetails("Sony PlayStation 1");
    }

    private string SonyPlayStation2Details()
    {
        return GetSystemDetails("Sony PlayStation 2");
    }

    private string SonyPlayStation3Details()
    {
        return GetSystemDetails("Sony PlayStation 3");
    }

    private string SonyPlayStation4Details()
    {
        return GetSystemDetails("Sony PlayStation 4");
    }

    private string SonyPlayStationVitaDetails()
    {
        return GetSystemDetails("Sony PlayStation Vita");
    }

    private string SonyPspDetails()
    {
        return GetSystemDetails("Sony PSP");
    }

    private string SuperAcanDetails()
    {
        return GetSystemDetails("Super Acan");
    }

    private string ZeeboDetails()
    {
        return GetSystemDetails("Zeebo");
    }

    private string GetSystemDetails(string systemName)
    {
        // Fetch the system details from the configuration
        var system =
            _manager.Systems.FirstOrDefault(s => s.SystemName.Contains(systemName, StringComparison.OrdinalIgnoreCase));

        var nodetailsavailablefor2 = (string)Application.Current.TryFindResource("Nodetailsavailablefor") ??
                                     "No details available for";
        return system?.SystemHelperText ?? $"{nodetailsavailablefor2} '{systemName}'.";
    }

    /// <summary>
    ///     Helper method to add plain text to a paragraph, processing raw URLs within it.
    /// </summary>
    /// <param name="paragraph">The paragraph to add inlines to.</param>
    /// <param name="text">The plain text segment to process.</param>
    private static void AddRawUrlsToParagraph(Paragraph paragraph, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var parts = RawUrlRegex().Split(text);
        var matches = RawUrlRegex().Matches(text);

        var matchIndex = 0;
        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part)) paragraph.Inlines.Add(new Run(part));

            if (matchIndex < matches.Count)
            {
                var rawUrl = matches[matchIndex].Value;
                var rawHyperlink = new Hyperlink(new Run(rawUrl))
                {
                    NavigateUri = new Uri(rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? rawUrl
                        : "http://" + rawUrl)
                };
                rawHyperlink.RequestNavigate += static (_, e) =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    });
                };
                paragraph.Inlines.Add(rawHyperlink);
                matchIndex++;
            }
        }
    }

    /// <summary>
    ///     Parses the input text for Markdown formatting (bold, headings, Markdown links, raw URLs)
    ///     and displays it in the provided RichTextBox.
    /// </summary>
    /// <param name="richTextBox">The RichTextBox to display the formatted text in.</param>
    /// <param name="text">The text containing Markdown to parse.</param>
    private static void SetTextWithMarkdownInternal(RichTextBox richTextBox, string text)
    {
        var flowDocument = new FlowDocument();
        var paragraph = new Paragraph();

        // Remove <br> tags
        text = text.Replace("<br>", "");

        // 1. Process headings first, converting them to bold syntax for subsequent bold processing
        text = HeadingRegex().Replace(text, static match => $"**{match.Groups[1].Value.Trim()}**");

        var matches = new List<(Match Match, string Type)>();

        // Find all bold matches
        foreach (Match match in BoldRegex().Matches(text)) matches.Add((match, "bold"));

        // Find all markdown link matches
        foreach (Match match in MarkdownLinkRegex().Matches(text)) matches.Add((match, "markdownLink"));

        // Sort matches by their starting index
        // This is crucial for correct processing order and handling overlaps (e.g., a markdown link
        // will take precedence over a raw URL that might be part of its URL component).
        matches.Sort(static (a, b) => a.Match.Index.CompareTo(b.Match.Index));

        var lastIndex = 0;

        foreach (var (match, type) in matches)
        {
            // Skip matches that overlap an already-emitted region: a markdown link nested in a
            // bold segment (e.g. **[click](url)**) starts before the bold match ends and used
            // to be emitted a second time, duplicating text and leaving stray ** markers.
            if (match.Index < lastIndex) continue;

            // Add plain text (and any raw URLs within it) before the current match
            if (match.Index > lastIndex)
            {
                var plainTextSegment = text.Substring(lastIndex, match.Index - lastIndex);
                AddRawUrlsToParagraph(paragraph, plainTextSegment);
            }

            // Add the formatted text based on type
            switch (type)
            {
                case "bold":
                    paragraph.Inlines.Add(new Bold(new Run(match.Groups[1].Value)));
                    break;
                case "markdownLink":
                    var linkText = match.Groups["text"].Value;
                    var url = match.Groups["url"].Value;
                    var hyperlink = new Hyperlink(new Run(linkText))
                    {
                        NavigateUri = new Uri(url)
                    };
                    hyperlink.RequestNavigate += static (_, e) =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = e.Uri.AbsoluteUri,
                            UseShellExecute = true
                        });
                    };
                    paragraph.Inlines.Add(hyperlink);
                    break;
            }

            lastIndex = match.Index + match.Length;
        }

        // Add any remaining plain text (and raw URLs within it) after the last match
        if (lastIndex < text.Length)
        {
            var remainingText = text[lastIndex..];
            AddRawUrlsToParagraph(paragraph, remainingText);
        }

        flowDocument.Blocks.Add(paragraph);
        richTextBox.Document = flowDocument;
    }
}