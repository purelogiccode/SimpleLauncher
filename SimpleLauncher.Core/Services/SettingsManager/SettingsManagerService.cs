using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.SettingsManager.EmulatorSettings;
using SimpleLauncher.Core.Services.UnifiedSettings;

namespace SimpleLauncher.Core.Services.SettingsManager;

/// <summary>
///     Manages application and emulator settings, providing thread-safe load/save operations against an XML configuration
///     file.
/// </summary>
public class SettingsManagerService : IDisposable
{
    /// <summary>Gets or sets the default horizontal dead zone value.</summary>
    public const float DefaultDeadZoneX = 0.05f;

    /// <summary>Gets or sets the default vertical dead zone value.</summary>
    public const float DefaultDeadZoneY = 0.02f;

    private const string DefaultSettingsFilePath = "settings.xml";
    private const string DefaultNotificationSoundFileName = "click.mp3";
    private const string EncryptedPrefix = "DPAPI:";
    private readonly IConfiguration _configuration;
    private readonly ICredentialProtector _credentialProtector;

    private readonly DataFileLocation _fileLocation;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;

    /// <summary>
    ///     Gets whether this instance persists to the unified SQLite database
    ///     (<c>settings.dat</c> in AppData) instead of the legacy <c>settings.xml</c>.
    ///     Opt-in per app: both the WPF and the Avalonia app pass <c>true</c>; the default
    ///     <c>false</c> keeps the legacy XML backend (used by tests and the migrator).
    /// </summary>
    public bool UseUnifiedDatabase { get; }
    private readonly ReaderWriterLockSlim _settingsLock = new(LockRecursionPolicy.SupportsRecursion);

    // AV-24: serializes concurrent SaveAsync calls. Every save snapshots under
    // the read lock but shares one temp path for the atomic write+move — without
    // serialization overlapping saves interleave temp writes and the loser can
    // overwrite a newer snapshot (lost toggles) or fail the move mid-shutdown.
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);

    private readonly HashSet<string> _validAccentColors =
    [
        "Amber", "Blue", "Brown", "Cobalt", "Crimson", "Cyan", "Emerald", "Green", "Indigo", "Lime", "Magenta",
        "Maroon", "Mauve", "Olive", "OliveDrab", "Orange", "Pink", "Plum", "Purple", "Red", "Sienna", "SkyBlue",
        "Steel", "Taupe", "Teal", "Violet", "Yellow"
    ];

    private readonly HashSet<string> _validBaseThemes = ["Light", "Dark", "Adaptive", "HighContrast", "Midnight"];

    private readonly HashSet<string> _validButtonAspectRatio =
        ["Square", "Wider", "SuperWider", "SuperWider2", "Taller", "SuperTaller", "SuperTaller2"];

    private readonly HashSet<string> _validFilenameDisplayModes = ["Original", "CleanUp", "NoFilename"];
    private readonly HashSet<string> _validFontSizes = ["Small", "Normal", "Big"];
    private readonly HashSet<int> _validGamesPerPage = [100, 200, 300, 400, 500, 1000, 10000, 1000000];
    private readonly HashSet<string> _validShowGames = ["ShowAll", "ShowWithCover", "ShowWithoutCover"];
    private readonly HashSet<string> _validStyleVariants = ["Default"];

    private readonly HashSet<int> _validThumbnailSizes =
        [50, 100, 150, 200, 250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 750, 800];

    private readonly HashSet<int> _validThumbnailSizesForSystem = [50, 100, 150];
    private readonly HashSet<string> _validViewModes = ["GridView", "ListView"];

    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the SettingsManagerService with the specified dependencies.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logErrors">The logger for error reporting.</param>
    /// <param name="credentialProtector">The credential protector for sensitive values.</param>
    /// <param name="messageBox">The message-box service (optional).</param>
    /// <param name="useUnifiedDatabase">
    ///     When true, settings persist to the unified SQLite database (<c>settings.dat</c> in AppData)
    ///     instead of the legacy <c>settings.xml</c>. Both the WPF and the Avalonia app opt in.
    /// </param>
    public SettingsManagerService(IConfiguration configuration, ILogger logErrors,
        ICredentialProtector credentialProtector, IMessageBoxLibraryService? messageBox = null,
        bool useUnifiedDatabase = false)
    {
        _configuration = configuration;
        _logger = logErrors;
        _credentialProtector = credentialProtector;
        _messageBox = messageBox!;
        UseUnifiedDatabase = useUnifiedDatabase;
        _fileLocation = new DataFileLocation(DefaultSettingsFilePath);

        VideoUrl = configuration.GetValue<string>("Urls:YouTubeSearch") ??
                   "https://www.youtube.com/results?search_query=";
        InfoUrl = configuration.GetValue<string>("Urls:IgdbSearch") ?? "https://www.igdb.com/search?q=";
    }

    // Application Settings
    /// <summary>Gets or sets the thumbnail size in pixels for game grid items.</summary>
    public int ThumbnailSize { get; set; } = 250;

    /// <summary>Gets or sets the thumbnail size in pixels for system list items.</summary>
    public int ThumbnailSizeForSystem { get; set; } = 50;

    /// <summary>Gets or sets the number of games displayed per page.</summary>
    public int GamesPerPage { get; set; } = 200;

    /// <summary>Gets or sets the game visibility filter (e.g., "ShowAll", "ShowWithCover").</summary>
    public string ShowGames { get; set; } = "ShowAll";

    /// <summary>Gets or sets the view mode (e.g., "GridView", "ListView").</summary>
    public string ViewMode { get; set; } = "GridView";

    /// <summary>Gets or sets whether gamepad navigation is enabled.</summary>
    public bool EnableGamePadNavigation { get; set; }

    /// <summary>Gets or sets the URL template for video search links.</summary>
    public string VideoUrl { get; set; }

    /// <summary>Gets or sets the URL template for information search links.</summary>
    public string InfoUrl { get; set; }

    /// <summary>Gets or sets the base UI theme (e.g., "Light", "Dark").</summary>
    public string BaseTheme { get; set; } = "Dark";

    /// <summary>Gets or sets the accent color name (e.g., "Blue", "Amber").</summary>
    public string AccentColor { get; set; } = "Blue";

    /// <summary>Gets or sets the style variant name.</summary>
    public string StyleVariant { get; set; } = "Default";

    /// <summary>Gets or sets the UI language code (e.g., "en").</summary>
    public string Language { get; set; } = "en";

    /// <summary>Gets or sets the horizontal gamepad dead zone threshold.</summary>
    public float DeadZoneX { get; set; } = DefaultDeadZoneX;

    /// <summary>Gets or sets the vertical gamepad dead zone threshold.</summary>
    public float DeadZoneY { get; set; } = DefaultDeadZoneY;

    /// <summary>Gets or sets the button aspect ratio for the game grid (e.g., "Square", "Wider").</summary>
    public string ButtonAspectRatio { get; set; } = "Square";

    /// <summary>Gets or sets how filenames are displayed (e.g., "Original", "CleanUp", "NoFilename").</summary>
    public string FilenameDisplayMode { get; set; } = "Original";

    /// <summary>Gets or sets whether the machine/system name is displayed on game items.</summary>
    public bool DisplayMachineName { get; set; }

    /// <summary>Gets or sets the font size for filenames (e.g., "Small", "Normal", "Big").</summary>
    public string FilenameFontSize { get; set; } = "Normal";

    /// <summary>Gets or sets the font size for machine names (e.g., "Small", "Normal", "Big").</summary>
    public string MachineNameFontSize { get; set; } = "Normal";

    /// <summary>Gets or sets whether fuzzy matching is used for image and name lookups.</summary>
    public bool EnableFuzzyMatching { get; set; } = true;

    /// <summary>Gets or sets the Jaro-Winkler similarity threshold for fuzzy matching (0.0–1.0).</summary>
    public double FuzzyMatchingThreshold { get; set; } = 0.80;

    /// <summary>
    ///     Gets or sets whether parenthetical annotations (e.g. region, language) are stripped from filenames before
    ///     matching images.
    /// </summary>
    public bool EnableAnnotationStripping { get; set; } = true;

    /// <summary>Gets or sets whether notification sounds are enabled.</summary>
    public bool EnableNotificationSound { get; set; } = true;

    /// <summary>Gets or sets the custom notification sound file path.</summary>
    public string CustomNotificationSoundFile { get; set; } = DefaultNotificationSoundFileName;

    /// <summary>Gets or sets the RetroAchievements username.</summary>
    public string RaUsername { get; set; } = "";

    /// <summary>Gets or sets the RetroAchievements API key.</summary>
    public string RaApiKey { get; set; } = "";

    /// <summary>Gets or sets the RetroAchievements password.</summary>
    public string RaPassword { get; set; } = "";

    /// <summary>Gets or sets the RetroAchievements token.</summary>
    public string RaToken { get; set; } = "";

    /// <summary>Gets or sets whether the RetroAchievements overlay button is visible.</summary>
    public bool OverlayRetroAchievementButton { get; set; }

    /// <summary>Gets or sets whether the open video overlay button is visible.</summary>
    public bool OverlayOpenVideoButton { get; set; } = true;

    /// <summary>Gets or sets whether the open info overlay button is visible.</summary>
    public bool OverlayOpenInfoButton { get; set; }

    /// <summary>Gets or sets whether the additional system folders section is expanded.</summary>
    public bool AdditionalSystemFoldersExpanded { get; set; } = true;

    /// <summary>Gets or sets whether emulator slot 1 settings are expanded.</summary>
    public bool Emulator1Expanded { get; set; } = true;

    /// <summary>Gets or sets whether emulator slot 2 settings are expanded.</summary>
    public bool Emulator2Expanded { get; set; } = true;

    /// <summary>Gets or sets whether emulator slot 3 settings are expanded.</summary>
    public bool Emulator3Expanded { get; set; } = true;

    /// <summary>Gets or sets whether emulator slot 4 settings are expanded.</summary>
    public bool Emulator4Expanded { get; set; } = true;

    /// <summary>Gets or sets whether emulator slot 5 settings are expanded.</summary>
    public bool Emulator5Expanded { get; set; } = true;

    /// <summary>Gets or sets the list of per-system play time records.</summary>
    public IList<SystemPlayTime> SystemPlayTimes { get; set; } = [];

    // Emulator Settings (composition)
    /// <summary>Gets or sets the Ares emulator configuration.</summary>
    public AresSettings Ares { get; set; } = new();

    /// <summary>Gets or sets the Azahar emulator configuration.</summary>
    public AzaharSettings Azahar { get; set; } = new();

    /// <summary>Gets or sets the BlastEm emulator configuration.</summary>
    public BlastemSettings Blastem { get; set; } = new();

    /// <summary>Gets or sets the Cemu emulator configuration.</summary>
    public CemuSettings Cemu { get; set; } = new();

    /// <summary>Gets or sets the Daphne emulator configuration.</summary>
    public DaphneSettings Daphne { get; set; } = new();

    /// <summary>Gets or sets the Dolphin emulator configuration.</summary>
    public DolphinSettings Dolphin { get; set; } = new();

    /// <summary>Gets or sets the DuckStation emulator configuration.</summary>
    public DuckStationSettings DuckStation { get; set; } = new();

    /// <summary>Gets or sets the Flycast emulator configuration.</summary>
    public FlycastSettings Flycast { get; set; } = new();

    /// <summary>Gets or sets the MAME emulator configuration.</summary>
    public MameSettings Mame { get; set; } = new();

    /// <summary>Gets or sets the Mednafen emulator configuration.</summary>
    public MednafenSettings Mednafen { get; set; } = new();

    /// <summary>Gets or sets the Mesen emulator configuration.</summary>
    public MesenSettings Mesen { get; set; } = new();

    /// <summary>Gets or sets the PCSX2 emulator configuration.</summary>
    public Pcsx2Settings Pcsx2 { get; set; } = new();

    /// <summary>Gets or sets the Raine emulator configuration.</summary>
    public RaineSettings Raine { get; set; } = new();

    /// <summary>Gets or sets the Redream emulator configuration.</summary>
    public RedreamSettings Redream { get; set; } = new();

    /// <summary>Gets or sets the RetroArch emulator configuration.</summary>
    public RetroArchSettings RetroArch { get; set; } = new();

    /// <summary>Gets or sets the RPCS3 emulator configuration.</summary>
    public Rpcs3Settings Rpcs3 { get; set; } = new();

    /// <summary>Gets or sets the Sega Model 2 emulator configuration.</summary>
    public SegaModel2Settings SegaModel2 { get; set; } = new();

    /// <summary>Gets or sets the Stella emulator configuration.</summary>
    public StellaSettings Stella { get; set; } = new();

    /// <summary>Gets or sets the Supermodel emulator configuration.</summary>
    public SupermodelSettings Supermodel { get; set; } = new();

    /// <summary>Gets or sets the Xenia emulator configuration.</summary>
    public XeniaSettings Xenia { get; set; } = new();

    /// <summary>Gets or sets the Yumir emulator configuration.</summary>
    public YumirSettings Yumir { get; set; } = new();

    /// <summary>Gets whether the settings file is stored in portable mode (next to the executable).</summary>
    public bool IsPortableMode => _fileLocation.IsPortableMode;

    /// <summary>
    ///     Releases resources used by the SettingsManagerService.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        _settingsLock?.Dispose();
        _saveSemaphore?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Loads settings from an explicit legacy XML file path without side effects.
    ///     Used by the one-time legacy migration (test seam: arbitrary folders).
    ///     When the file is missing or corrupt the current values are kept and no
    ///     file is created (unlike <see cref="Load" />).
    /// </summary>
    /// <returns>
    ///     True when the file existed and was parsed; false when it is missing or unreadable
    ///     (callers must not treat the untouched defaults as user data in that case).
    /// </returns>
    internal bool LoadFromLegacyFile(string path)
    {
        XElement? settings = null;

        if (File.Exists(path))
        {
            try
            {
                var readerSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using var reader = XmlReader.Create(path, readerSettings);
                settings = XElement.Load(reader);
            }
            catch (Exception ex)
            {
                // Expected user-data condition (a corrupt legacy file simply imports as
                // empty): Information level so it is never reported as a bug.
                _logger.Information(ex, "Error loading legacy settings file '{Path}'", path);
            }
        }

        if (settings is null)
            return false;

        _settingsLock.EnterWriteLock();
        try
        {
            LoadFromXml(settings);
        }
        finally
        {
            _settingsLock.ExitWriteLock();
        }

        return true;
    }

    private string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        try
        {
            return _credentialProtector.Protect(plainText);
        }
        catch (Exception ex)
        {
            // Security-relevant: the credential would previously be persisted in plain text
            // without any indication. Log at Warning (a genuine DPAPI/environment failure, not
            // an expected user condition) and refuse to persist it unencrypted.
            _logger.Warning(ex,
                "Credential protection failed; the credential will not be persisted in plain text");
            return string.Empty;
        }
    }

    private string? DecryptString(string storedValue)
    {
        if (string.IsNullOrEmpty(storedValue)) return storedValue;

        if (storedValue.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            var legacyValue = storedValue[EncryptedPrefix.Length..];
            try
            {
                return _credentialProtector.Unprotect(legacyValue);
            }
            catch
            {
                return legacyValue;
            }
        }

        try
        {
            return _credentialProtector.Unprotect(storedValue);
        }
        catch
        {
            return storedValue;
        }
    }

    /// <summary>
    ///     Loads settings, applying defaults if no persisted state exists.
    ///     In unified-database mode this reads <c>settings.dat</c> (falling back to the legacy
    ///     <c>settings.xml</c> when the database does not exist yet so the migrator can import it);
    ///     otherwise it reads the legacy <c>settings.xml</c> exactly as before.
    /// </summary>
    public void Load()
    {
        if (UseUnifiedDatabase && UnifiedSettingsDatabase.IsValidDatabase())
        {
            try
            {
                LoadFromDatabase();
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading settings from the unified database; falling back to legacy files");
            }
        }

        XElement? settings = null;

        // Read from disk without holding any lock — disk I/O is slow and
        // does not mutate shared state, so concurrent readers are unaffected.
        if (File.Exists(_fileLocation.FilePath))
        {
            try
            {
                // Harden against XXE: prohibit DTDs and external resolution, matching
                // SystemConfigurationWriterService and EasyModeManager (CORE-27).
                var readerSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using var reader = XmlReader.Create(_fileLocation.FilePath, readerSettings);
                settings = XElement.Load(reader);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading settings.xml");
            }
        }

        // Take a write lock only for the in-memory property updates.
        _settingsLock.EnterWriteLock();
        try
        {
            if (settings != null)
                LoadFromXml(settings);
            else
                SetDefaultsAndSave();
        }
        finally
        {
            _settingsLock.ExitWriteLock();
        }
    }

    private void CopyFrom(SettingsManagerService other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Application Settings
        ThumbnailSize = other.ThumbnailSize;
        ThumbnailSizeForSystem = other.ThumbnailSizeForSystem;
        GamesPerPage = other.GamesPerPage;
        ShowGames = other.ShowGames;
        ViewMode = other.ViewMode;
        EnableGamePadNavigation = other.EnableGamePadNavigation;
        VideoUrl = other.VideoUrl;
        InfoUrl = other.InfoUrl;
        BaseTheme = other.BaseTheme;
        AccentColor = other.AccentColor;
        StyleVariant = other.StyleVariant;
        Language = other.Language;
        DeadZoneX = other.DeadZoneX;
        DeadZoneY = other.DeadZoneY;
        ButtonAspectRatio = other.ButtonAspectRatio;
        FilenameDisplayMode = other.FilenameDisplayMode;
        DisplayMachineName = other.DisplayMachineName;
        FilenameFontSize = other.FilenameFontSize;
        MachineNameFontSize = other.MachineNameFontSize;
        EnableFuzzyMatching = other.EnableFuzzyMatching;
        FuzzyMatchingThreshold = other.FuzzyMatchingThreshold;
        EnableAnnotationStripping = other.EnableAnnotationStripping;
        EnableNotificationSound = other.EnableNotificationSound;
        CustomNotificationSoundFile = other.CustomNotificationSoundFile;
        RaUsername = other.RaUsername;
        RaApiKey = other.RaApiKey;
        RaPassword = other.RaPassword;
        RaToken = other.RaToken;
        OverlayRetroAchievementButton = other.OverlayRetroAchievementButton;
        OverlayOpenVideoButton = other.OverlayOpenVideoButton;
        OverlayOpenInfoButton = other.OverlayOpenInfoButton;
        AdditionalSystemFoldersExpanded = other.AdditionalSystemFoldersExpanded;
        Emulator1Expanded = other.Emulator1Expanded;
        Emulator2Expanded = other.Emulator2Expanded;
        Emulator3Expanded = other.Emulator3Expanded;
        Emulator4Expanded = other.Emulator4Expanded;
        Emulator5Expanded = other.Emulator5Expanded;

        SystemPlayTimes = other.SystemPlayTimes?
            .Select(static pt => new SystemPlayTime
                { SystemName = pt.SystemName, PlayTimeSeconds = pt.PlayTimeSeconds })
            .ToList() ?? [];

        // Emulator Settings (delegate to each emulator's CopyFrom)
        Ares.CopyFrom(other.Ares);
        Azahar.CopyFrom(other.Azahar);
        Blastem.CopyFrom(other.Blastem);
        Cemu.CopyFrom(other.Cemu);
        Daphne.CopyFrom(other.Daphne);
        Dolphin.CopyFrom(other.Dolphin);
        DuckStation.CopyFrom(other.DuckStation);
        Flycast.CopyFrom(other.Flycast);
        Mame.CopyFrom(other.Mame);
        Mednafen.CopyFrom(other.Mednafen);
        Mesen.CopyFrom(other.Mesen);
        Pcsx2.CopyFrom(other.Pcsx2);
        Raine.CopyFrom(other.Raine);
        Redream.CopyFrom(other.Redream);
        RetroArch.CopyFrom(other.RetroArch);
        Rpcs3.CopyFrom(other.Rpcs3);
        SegaModel2.CopyFrom(other.SegaModel2);
        Stella.CopyFrom(other.Stella);
        Supermodel.CopyFrom(other.Supermodel);
        Xenia.CopyFrom(other.Xenia);
        Yumir.CopyFrom(other.Yumir);
    }

    private void LoadFromXml(XElement settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Application Settings Fallback Logic
        var app = settings.Element("Application");
        LoadApplicationSection(app, settings);
        LoadRemainingSections(settings);
    }

    /// <summary>
    ///     Applies the Application section. Shared by the XML loader and the unified-database
    ///     importer so both paths enforce identical validation. Missing keys keep their current
    ///     values; invalid values fall back to safe defaults.
    /// </summary>
    private void LoadApplicationSection(XElement? app, XElement settings)
    {
        ThumbnailSize =
            ValidateThumbnailSize(
                app?.Element("ThumbnailSize")?.Value ?? settings.Element("ThumbnailSize")?.Value ?? "");
        ThumbnailSizeForSystem = ValidateThumbnailSizeForSystem(app?.Element("ThumbnailSizeForSystem")?.Value ??
                                                                settings.Element("ThumbnailSizeForSystem")?.Value ??
                                                                "");
        GamesPerPage =
            ValidateGamesPerPage(app?.Element("GamesPerPage")?.Value ?? settings.Element("GamesPerPage")?.Value ?? "");
        ShowGames = ValidateShowGames(app?.Element("ShowGames")?.Value ?? settings.Element("ShowGames")?.Value ?? "");
        ViewMode = ValidateViewMode(app?.Element("ViewMode")?.Value ?? settings.Element("ViewMode")?.Value ?? "");

        if (bool.TryParse(
                app?.Element("EnableGamePadNavigation")?.Value ?? settings.Element("EnableGamePadNavigation")?.Value,
                out var gp))
        {
            EnableGamePadNavigation = gp;
        }

        VideoUrl = app?.Element("VideoUrl")?.Value ?? settings.Element("VideoUrl")?.Value ?? VideoUrl;
        InfoUrl = app?.Element("InfoUrl")?.Value ?? settings.Element("InfoUrl")?.Value ?? InfoUrl;
        BaseTheme = ValidateBaseTheme(app?.Element("BaseTheme")?.Value ??
                                      settings.Element("BaseTheme")?.Value ?? BaseTheme);
        AccentColor = ValidateAccentColor(app?.Element("AccentColor")?.Value ??
                                          settings.Element("AccentColor")?.Value ?? AccentColor);
        StyleVariant =
            ValidateStyleVariant(app?.Element("StyleVariant")?.Value ?? settings.Element("StyleVariant")?.Value ?? "");
        Language = app?.Element("Language")?.Value ?? settings.Element("Language")?.Value ?? Language;
        ButtonAspectRatio = ValidateButtonAspectRatio(app?.Element("ButtonAspectRatio")?.Value ??
                                                      settings.Element("ButtonAspectRatio")?.Value ?? "");
        FilenameDisplayMode = ValidateFilenameDisplayMode(app?.Element("FilenameDisplayMode")?.Value ??
                                                          settings.Element("FilenameDisplayMode")?.Value ?? "");
        if (bool.TryParse(app?.Element("DisplayMachineName")?.Value ?? settings.Element("DisplayMachineName")?.Value,
                out var dmn))
        {
            DisplayMachineName = dmn;
        }

        FilenameFontSize = ValidateFontSize(app?.Element("FilenameFontSize")?.Value ??
                                            settings.Element("FilenameFontSize")?.Value ?? "");
        MachineNameFontSize = ValidateFontSize(app?.Element("MachineNameFontSize")?.Value ??
                                               settings.Element("MachineNameFontSize")?.Value ?? "");

        RaUsername = app?.Element("RaUsername")?.Value ?? settings.Element("RaUsername")?.Value ??
            settings.Element("RA_Username")?.Value ?? RaUsername;
        RaApiKey = DecryptString(app?.Element("RaApiKey")?.Value ??
                                 settings.Element("RaApiKey")?.Value ??
                                 settings.Element("RA_ApiKey")?.Value ?? RaApiKey) ?? "";
        RaPassword =
            DecryptString(app?.Element("RaPassword")?.Value ?? settings.Element("RaPassword")?.Value ?? RaPassword) ??
            "";
        RaToken = DecryptString(app?.Element("RaToken")?.Value ?? settings.Element("RaToken")?.Value ?? RaToken) ?? "";

        if (float.TryParse(app?.Element("DeadZoneX")?.Value ?? settings.Element("DeadZoneX")?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var dzx))
        {
            DeadZoneX = dzx;
        }

        if (float.TryParse(app?.Element("DeadZoneY")?.Value ?? settings.Element("DeadZoneY")?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var dzy))
        {
            DeadZoneY = dzy;
        }

        if (bool.TryParse(app?.Element("EnableFuzzyMatching")?.Value ?? settings.Element("EnableFuzzyMatching")?.Value,
                out var fm))
        {
            EnableFuzzyMatching = fm;
        }

        if (double.TryParse(
                app?.Element("FuzzyMatchingThreshold")?.Value ?? settings.Element("FuzzyMatchingThreshold")?.Value,
                NumberStyles.Any, CultureInfo.InvariantCulture, out var fmt))
        {
            FuzzyMatchingThreshold = fmt;
        }

        if (bool.TryParse(
                app?.Element("EnableAnnotationStripping")?.Value ??
                settings.Element("EnableAnnotationStripping")?.Value, out var ans))
        {
            EnableAnnotationStripping = ans;
        }

        if (bool.TryParse(
                app?.Element("EnableNotificationSound")?.Value ?? settings.Element("EnableNotificationSound")?.Value,
                out var ens))
        {
            EnableNotificationSound = ens;
        }

        CustomNotificationSoundFile = app?.Element("CustomNotificationSoundFile")?.Value ??
                                      settings.Element("CustomNotificationSoundFile")?.Value ??
                                      CustomNotificationSoundFile;
        if (bool.TryParse(
                app?.Element("OverlayRetroAchievementButton")?.Value ??
                settings.Element("OverlayRetroAchievementButton")?.Value, out var ora))
        {
            OverlayRetroAchievementButton = ora;
        }

        if (bool.TryParse(
                app?.Element("OverlayOpenVideoButton")?.Value ?? settings.Element("OverlayOpenVideoButton")?.Value,
                out var ovb))
        {
            OverlayOpenVideoButton = ovb;
        }

        if (bool.TryParse(
                app?.Element("OverlayOpenInfoButton")?.Value ?? settings.Element("OverlayOpenInfoButton")?.Value,
                out var oib))
        {
            OverlayOpenInfoButton = oib;
        }

        if (bool.TryParse(
                app?.Element("AdditionalSystemFoldersExpanded")?.Value ??
                settings.Element("AdditionalSystemFoldersExpanded")?.Value, out var asfe))
        {
            AdditionalSystemFoldersExpanded = asfe;
        }

        if (bool.TryParse(app?.Element("Emulator1Expanded")?.Value ?? settings.Element("Emulator1Expanded")?.Value,
                out var e1E))
        {
            Emulator1Expanded = e1E;
        }

        if (bool.TryParse(app?.Element("Emulator2Expanded")?.Value ?? settings.Element("Emulator2Expanded")?.Value,
                out var e2E))
        {
            Emulator2Expanded = e2E;
        }

        if (bool.TryParse(app?.Element("Emulator3Expanded")?.Value ?? settings.Element("Emulator3Expanded")?.Value,
                out var e3E))
        {
            Emulator3Expanded = e3E;
        }

        if (bool.TryParse(app?.Element("Emulator4Expanded")?.Value ?? settings.Element("Emulator4Expanded")?.Value,
                out var e4E))
        {
            Emulator4Expanded = e4E;
        }

        if (bool.TryParse(app?.Element("Emulator5Expanded")?.Value ?? settings.Element("Emulator5Expanded")?.Value,
                out var e5E))
        {
            Emulator5Expanded = e5E;
        }
    }

    /// <summary>Loads emulator sections and per-system play times (XML loader path).</summary>
    private void LoadRemainingSections(XElement settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Delegate emulator settings loading to each emulator's LoadFromXml
        Ares.LoadFromXml(settings);
        Azahar.LoadFromXml(settings);
        Blastem.LoadFromXml(settings);
        Cemu.LoadFromXml(settings);
        Daphne.LoadFromXml(settings);
        Dolphin.LoadFromXml(settings);
        DuckStation.LoadFromXml(settings);
        Flycast.LoadFromXml(settings);
        Mame.LoadFromXml(settings);
        Mednafen.LoadFromXml(settings);
        Mesen.LoadFromXml(settings);
        Pcsx2.LoadFromXml(settings);
        Raine.LoadFromXml(settings);
        Redream.LoadFromXml(settings);
        RetroArch.LoadFromXml(settings);
        Rpcs3.LoadFromXml(settings);
        SegaModel2.LoadFromXml(settings);
        Stella.LoadFromXml(settings);
        Supermodel.LoadFromXml(settings);
        Xenia.LoadFromXml(settings);
        Yumir.LoadFromXml(settings);

        // SystemPlayTimes
        var playTimes = settings.Element("SystemPlayTimes");
        if (playTimes != null)
        {
            SystemPlayTimes.Clear();
            foreach (var pt in playTimes.Elements("SystemPlayTime"))
            {
                var playTimeValue = pt.Element("PlayTime")?.Value ?? "0";
                if (long.TryParse(playTimeValue, CultureInfo.InvariantCulture, out var seconds))
                {
                    // New format: stored as total seconds
                }
                else if (TimeSpan.TryParse(playTimeValue, CultureInfo.InvariantCulture, out var ts))
                {
                    seconds = (long)ts.TotalSeconds;
                }
                else
                {
                    seconds = 0;
                }

                SystemPlayTimes.Add(new SystemPlayTime
                {
                    SystemName = pt.Element("SystemName")?.Value ?? "",
                    PlayTimeSeconds = seconds
                });
            }
        }
    }

    /// <summary>
    ///     Asynchronously saves the current settings to the XML configuration file with retry logic.
    ///     Concurrent calls are serialized (AV-24) so overlapping saves can neither
    ///     interleave on the shared temp path nor overwrite a newer snapshot.
    /// </summary>
    public Task SaveAsync()
    {
        SettingsManagerService snapshot;
        _settingsLock.EnterReadLock();
        try
        {
            snapshot = new SettingsManagerService(_configuration, _logger, _credentialProtector, _messageBox,
                UseUnifiedDatabase);
            snapshot.CopyFrom(this);
        }
        finally
        {
            _settingsLock.ExitReadLock();
        }

        return Task.Run(async () =>
        {
            try
            {
                await _saveSemaphore.WaitAsync();
            }
            catch (ObjectDisposedException)
            {
                return; // Service is shutting down; the save cannot proceed.
            }

            try
            {
                await WriteSnapshotAsync(snapshot);
            }
            catch (Exception ex)
            {
                // Settings persistence is best-effort and usually fire-and-forget:
                // never let a failed background save fault the returned task (it would
                // surface later as an unobserved task exception / bug report). A
                // read-only or locked settings.dat is an environment issue, not a bug.
                _logger.Information(ex, "Failed to save settings");
            }
            finally
            {
                try
                {
                    _saveSemaphore.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Disposed mid-save during shutdown — nothing left to release to.
                }
            }
        });
    }

    private async Task WriteSnapshotAsync(SettingsManagerService snapshot)
    {
        if (snapshot.UseUnifiedDatabase)
        {
            await Task.Run(() => snapshot.SaveToDatabase());
            return;
        }

            var tempPath = _fileLocation.TempFilePath;
            const int maxRetries = 3;
            var retryDelayMs = 500;
            Exception? lastException = null;

            var settingsDirectory = Path.GetDirectoryName(_fileLocation.FilePath);
            if (!string.IsNullOrEmpty(settingsDirectory) && !Directory.Exists(settingsDirectory))
            {
                try
                {
                    Directory.CreateDirectory(settingsDirectory);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error creating settings directory");
                }
            }

            var attempt = 0;
            while (attempt < maxRetries)
            {
                try
                {
                    var root = BuildXElement(snapshot);

                    byte[] xmlBytes;
                    using (var ms = new MemoryStream())
                    {
                        root.Save(ms);
                        xmlBytes = ms.ToArray();
                    }

                    if (xmlBytes.Length == 0) throw new InvalidOperationException("Generated settings XML is empty.");

                    await File.WriteAllBytesAsync(tempPath, xmlBytes);
                    File.Move(tempPath, _fileLocation.FilePath, true);
                    return;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    lastException = ex;
                    attempt++;

                    if (IsPortableMode && attempt >= maxRetries)
                    {
                        try
                        {
                            if (_fileLocation.TryFallbackToLocalAppData())
                            {
                                tempPath = _fileLocation.TempFilePath;
                                attempt = 0;
                                continue;
                            }
                        }
                        catch
                        {
                            // Fallback failed
                        }
                    }

                    if (attempt < maxRetries)
                    {
                        try
                        {
                            if (File.Exists(tempPath)) File.Delete(tempPath);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }

                        Thread.Sleep(retryDelayMs);
                        retryDelayMs *= 2;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }
            }

            _logger.Error(lastException, "Error saving settings.xml");

            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            if (_messageBox != null) await _messageBox.FailedToSaveSettingsMessageBoxAsync();
    }

    private static XElement BuildXElement(SettingsManagerService s)
    {
        return new XElement("Settings",
            // Application Settings
            new XElement("Application",
                new XElement("ThumbnailSize", s.ThumbnailSize),
                new XElement("ThumbnailSizeForSystem", s.ThumbnailSizeForSystem),
                new XElement("GamesPerPage", s.GamesPerPage),
                new XElement("ShowGames", s.ShowGames),
                new XElement("ViewMode", s.ViewMode),
                new XElement("EnableGamePadNavigation", s.EnableGamePadNavigation),
                new XElement("VideoUrl", s.VideoUrl),
                new XElement("InfoUrl", s.InfoUrl),
                new XElement("BaseTheme", s.BaseTheme),
                new XElement("AccentColor", s.AccentColor),
                new XElement("StyleVariant", s.StyleVariant),
                new XElement("Language", s.Language),
                new XElement("DeadZoneX", s.DeadZoneX.ToString(CultureInfo.InvariantCulture)),
                new XElement("DeadZoneY", s.DeadZoneY.ToString(CultureInfo.InvariantCulture)),
                new XElement("ButtonAspectRatio", s.ButtonAspectRatio),
                new XElement("FilenameDisplayMode", s.FilenameDisplayMode),
                new XElement("DisplayMachineName", s.DisplayMachineName),
                new XElement("FilenameFontSize", s.FilenameFontSize),
                new XElement("MachineNameFontSize", s.MachineNameFontSize),
                new XElement("EnableFuzzyMatching", s.EnableFuzzyMatching),
                new XElement("FuzzyMatchingThreshold", s.FuzzyMatchingThreshold.ToString(CultureInfo.InvariantCulture)),
                new XElement("EnableAnnotationStripping", s.EnableAnnotationStripping),
                new XElement("EnableNotificationSound", s.EnableNotificationSound),
                new XElement("CustomNotificationSoundFile", s.CustomNotificationSoundFile),
                new XElement("RaUsername", s.RaUsername),
                new XElement("RaApiKey", s.EncryptString(s.RaApiKey)),
                new XElement("RaPassword", s.EncryptString(s.RaPassword)),
                new XElement("RaToken", s.EncryptString(s.RaToken)),
                new XElement("OverlayRetroAchievementButton", s.OverlayRetroAchievementButton),
                new XElement("OverlayOpenVideoButton", s.OverlayOpenVideoButton),
                new XElement("OverlayOpenInfoButton", s.OverlayOpenInfoButton),
                new XElement("AdditionalSystemFoldersExpanded", s.AdditionalSystemFoldersExpanded),
                new XElement("Emulator1Expanded", s.Emulator1Expanded),
                new XElement("Emulator2Expanded", s.Emulator2Expanded),
                new XElement("Emulator3Expanded", s.Emulator3Expanded),
                new XElement("Emulator4Expanded", s.Emulator4Expanded),
                new XElement("Emulator5Expanded", s.Emulator5Expanded)
            ),

            // Delegate emulator serialization to each emulator's ToXElement
            s.Ares.ToXElement(),
            s.Azahar.ToXElement(),
            s.Blastem.ToXElement(),
            s.Cemu.ToXElement(),
            s.Daphne.ToXElement(),
            s.Dolphin.ToXElement(),
            s.DuckStation.ToXElement(),
            s.Flycast.ToXElement(),
            s.Mame.ToXElement(),
            s.Mednafen.ToXElement(),
            s.Mesen.ToXElement(),
            s.Pcsx2.ToXElement(),
            s.Raine.ToXElement(),
            s.Redream.ToXElement(),
            s.RetroArch.ToXElement(),
            s.Rpcs3.ToXElement(),
            s.SegaModel2.ToXElement(),
            s.Stella.ToXElement(),
            s.Supermodel.ToXElement(),
            s.Xenia.ToXElement(),
            s.Yumir.ToXElement(),

            // SystemPlayTimes
            new XElement("SystemPlayTimes",
                s.SystemPlayTimes.Select(static pt =>
                    new XElement("SystemPlayTime",
                        new XElement("SystemName", pt.SystemName),
                        new XElement("PlayTime", pt.PlayTimeSeconds)
                    )
                )
            )
        );
    }

    private int ValidateThumbnailSize(string value)
    {
        return int.TryParse(value, CultureInfo.InvariantCulture, out var p) && _validThumbnailSizes.Contains(p)
            ? p
            : 250;
    }

    private int ValidateThumbnailSizeForSystem(string value)
    {
        return int.TryParse(value, CultureInfo.InvariantCulture, out var p) && _validThumbnailSizesForSystem.Contains(p)
            ? p
            : 50;
    }

    private int ValidateGamesPerPage(string value)
    {
        return int.TryParse(value, CultureInfo.InvariantCulture, out var p) && _validGamesPerPage.Contains(p) ? p : 200;
    }

    private string ValidateShowGames(string value)
    {
        return _validShowGames.Contains(value) ? value : "ShowAll";
    }

    private string ValidateViewMode(string value)
    {
        return _validViewModes.Contains(value) ? value : "GridView";
    }

    private string ValidateButtonAspectRatio(string value)
    {
        return _validButtonAspectRatio.Contains(value) ? value : "Square";
    }

    private string ValidateFilenameDisplayMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Original";

        return _validFilenameDisplayModes.Contains(value) ? value : "Original";
    }

    private string ValidateFontSize(string value)
    {
        return _validFontSizes.Contains(value) ? value : "Normal";
    }

    private string ValidateStyleVariant(string value)
    {
        return _validStyleVariants.Contains(value) ? value : "Default";
    }

    private string ValidateBaseTheme(string value)
    {
        return _validBaseThemes.Contains(value) ? value : "Dark";
    }

    private string ValidateAccentColor(string value)
    {
        return _validAccentColors.Contains(value) ? value : "Blue";
    }

    /// <summary>
    ///     Resets all settings to their default values.
    /// </summary>
    public void ResetToDefaults()
    {
        _settingsLock.EnterWriteLock();
        try
        {
            ResetToDefaultsLocked();
        }
        finally
        {
            _settingsLock.ExitWriteLock();
        }
    }

    // ── Unified-database backend (WPF/Avalonia opt in via useUnifiedDatabase) ──

    /// <summary>
    ///     Exports the Application section as a key/value dictionary (values use invariant
    ///     formatting; secrets stay encrypted exactly as they are written to XML).
    /// </summary>
    public Dictionary<string, string> ExportAppSettings()
    {
        _settingsLock.EnterReadLock();
        try
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ThumbnailSize"] = ThumbnailSize.ToString(CultureInfo.InvariantCulture),
                ["ThumbnailSizeForSystem"] = ThumbnailSizeForSystem.ToString(CultureInfo.InvariantCulture),
                ["GamesPerPage"] = GamesPerPage.ToString(CultureInfo.InvariantCulture),
                ["ShowGames"] = ShowGames,
                ["ViewMode"] = ViewMode,
                ["EnableGamePadNavigation"] = EnableGamePadNavigation.ToString(CultureInfo.InvariantCulture),
                ["VideoUrl"] = VideoUrl,
                ["InfoUrl"] = InfoUrl,
                ["BaseTheme"] = BaseTheme,
                ["AccentColor"] = AccentColor,
                ["StyleVariant"] = StyleVariant,
                ["Language"] = Language,
                ["DeadZoneX"] = DeadZoneX.ToString(CultureInfo.InvariantCulture),
                ["DeadZoneY"] = DeadZoneY.ToString(CultureInfo.InvariantCulture),
                ["ButtonAspectRatio"] = ButtonAspectRatio,
                ["FilenameDisplayMode"] = FilenameDisplayMode,
                ["DisplayMachineName"] = DisplayMachineName.ToString(CultureInfo.InvariantCulture),
                ["FilenameFontSize"] = FilenameFontSize,
                ["MachineNameFontSize"] = MachineNameFontSize,
                ["EnableFuzzyMatching"] = EnableFuzzyMatching.ToString(CultureInfo.InvariantCulture),
                ["FuzzyMatchingThreshold"] = FuzzyMatchingThreshold.ToString(CultureInfo.InvariantCulture),
                ["EnableAnnotationStripping"] = EnableAnnotationStripping.ToString(CultureInfo.InvariantCulture),
                ["EnableNotificationSound"] = EnableNotificationSound.ToString(CultureInfo.InvariantCulture),
                ["CustomNotificationSoundFile"] = CustomNotificationSoundFile,
                ["RaUsername"] = RaUsername,
                ["RaApiKey"] = EncryptString(RaApiKey),
                ["RaPassword"] = EncryptString(RaPassword),
                ["RaToken"] = EncryptString(RaToken),
                ["OverlayRetroAchievementButton"] =
                    OverlayRetroAchievementButton.ToString(CultureInfo.InvariantCulture),
                ["OverlayOpenVideoButton"] = OverlayOpenVideoButton.ToString(CultureInfo.InvariantCulture),
                ["OverlayOpenInfoButton"] = OverlayOpenInfoButton.ToString(CultureInfo.InvariantCulture),
                ["AdditionalSystemFoldersExpanded"] =
                    AdditionalSystemFoldersExpanded.ToString(CultureInfo.InvariantCulture),
                ["Emulator1Expanded"] = Emulator1Expanded.ToString(CultureInfo.InvariantCulture),
                ["Emulator2Expanded"] = Emulator2Expanded.ToString(CultureInfo.InvariantCulture),
                ["Emulator3Expanded"] = Emulator3Expanded.ToString(CultureInfo.InvariantCulture),
                ["Emulator4Expanded"] = Emulator4Expanded.ToString(CultureInfo.InvariantCulture),
                ["Emulator5Expanded"] = Emulator5Expanded.ToString(CultureInfo.InvariantCulture)
            };
        }
        finally
        {
            _settingsLock.ExitReadLock();
        }
    }

    /// <summary>
    ///     Imports Application-section values (same validation as the XML loader).
    ///     Missing keys keep their current values.
    /// </summary>
    public void ImportAppSettings(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var app = new XElement("Application",
            values.Select(static kvp => new XElement(kvp.Key, kvp.Value)));
        var root = new XElement("Settings", app);

        _settingsLock.EnterWriteLock();
        try
        {
            LoadApplicationSection(app, root);
        }
        finally
        {
            _settingsLock.ExitWriteLock();
        }
    }

    /// <summary>Exports every emulator settings object as a name → JSON dictionary.</summary>
    public Dictionary<string, string> ExportEmulatorSettings()
    {
        _settingsLock.EnterReadLock();
        try
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Ares"] = EmulatorSettingsJson.Serialize(Ares),
                ["Azahar"] = EmulatorSettingsJson.Serialize(Azahar),
                ["Blastem"] = EmulatorSettingsJson.Serialize(Blastem),
                ["Cemu"] = EmulatorSettingsJson.Serialize(Cemu),
                ["Daphne"] = EmulatorSettingsJson.Serialize(Daphne),
                ["Dolphin"] = EmulatorSettingsJson.Serialize(Dolphin),
                ["DuckStation"] = EmulatorSettingsJson.Serialize(DuckStation),
                ["Flycast"] = EmulatorSettingsJson.Serialize(Flycast),
                ["Mame"] = EmulatorSettingsJson.Serialize(Mame),
                ["Mednafen"] = EmulatorSettingsJson.Serialize(Mednafen),
                ["Mesen"] = EmulatorSettingsJson.Serialize(Mesen),
                ["Pcsx2"] = EmulatorSettingsJson.Serialize(Pcsx2),
                ["Raine"] = EmulatorSettingsJson.Serialize(Raine),
                ["Redream"] = EmulatorSettingsJson.Serialize(Redream),
                ["RetroArch"] = EmulatorSettingsJson.Serialize(RetroArch),
                ["Rpcs3"] = EmulatorSettingsJson.Serialize(Rpcs3),
                ["SegaModel2"] = EmulatorSettingsJson.Serialize(SegaModel2),
                ["Stella"] = EmulatorSettingsJson.Serialize(Stella),
                ["Supermodel"] = EmulatorSettingsJson.Serialize(Supermodel),
                ["Xenia"] = EmulatorSettingsJson.Serialize(Xenia),
                ["Yumir"] = EmulatorSettingsJson.Serialize(Yumir)
            };
        }
        finally
        {
            _settingsLock.ExitReadLock();
        }
    }

    /// <summary>
    ///     Imports emulator settings from a name → JSON dictionary.
    ///     Corrupt or absent entries keep their current values.
    /// </summary>
    public void ImportEmulatorSettings(IReadOnlyDictionary<string, string> configs)
    {
        ArgumentNullException.ThrowIfNull(configs);

        _settingsLock.EnterWriteLock();
        try
        {
            if (configs.TryGetValue("Ares", out var ares) &&
                EmulatorSettingsJson.Deserialize<AresSettings>(ares) is { } aresV) Ares.CopyFrom(aresV);
            if (configs.TryGetValue("Azahar", out var azahar) &&
                EmulatorSettingsJson.Deserialize<AzaharSettings>(azahar) is { } azaharV) Azahar.CopyFrom(azaharV);
            if (configs.TryGetValue("Blastem", out var blastem) &&
                EmulatorSettingsJson.Deserialize<BlastemSettings>(blastem) is { } blastemV) Blastem.CopyFrom(blastemV);
            if (configs.TryGetValue("Cemu", out var cemu) &&
                EmulatorSettingsJson.Deserialize<CemuSettings>(cemu) is { } cemuV) Cemu.CopyFrom(cemuV);
            if (configs.TryGetValue("Daphne", out var daphne) &&
                EmulatorSettingsJson.Deserialize<DaphneSettings>(daphne) is { } daphneV) Daphne.CopyFrom(daphneV);
            if (configs.TryGetValue("Dolphin", out var dolphin) &&
                EmulatorSettingsJson.Deserialize<DolphinSettings>(dolphin) is { } dolphinV) Dolphin.CopyFrom(dolphinV);
            if (configs.TryGetValue("DuckStation", out var duckStation) &&
                EmulatorSettingsJson.Deserialize<DuckStationSettings>(duckStation) is { } duckStationV)
                DuckStation.CopyFrom(duckStationV);
            if (configs.TryGetValue("Flycast", out var flycast) &&
                EmulatorSettingsJson.Deserialize<FlycastSettings>(flycast) is { } flycastV) Flycast.CopyFrom(flycastV);
            if (configs.TryGetValue("Mame", out var mame) &&
                EmulatorSettingsJson.Deserialize<MameSettings>(mame) is { } mameV) Mame.CopyFrom(mameV);
            if (configs.TryGetValue("Mednafen", out var mednafen) &&
                EmulatorSettingsJson.Deserialize<MednafenSettings>(mednafen) is { } mednafenV) Mednafen.CopyFrom(mednafenV);
            if (configs.TryGetValue("Mesen", out var mesen) &&
                EmulatorSettingsJson.Deserialize<MesenSettings>(mesen) is { } mesenV) Mesen.CopyFrom(mesenV);
            if (configs.TryGetValue("Pcsx2", out var pcsx2) &&
                EmulatorSettingsJson.Deserialize<Pcsx2Settings>(pcsx2) is { } pcsx2V) Pcsx2.CopyFrom(pcsx2V);
            if (configs.TryGetValue("Raine", out var raine) &&
                EmulatorSettingsJson.Deserialize<RaineSettings>(raine) is { } raineV) Raine.CopyFrom(raineV);
            if (configs.TryGetValue("Redream", out var redream) &&
                EmulatorSettingsJson.Deserialize<RedreamSettings>(redream) is { } redreamV) Redream.CopyFrom(redreamV);
            if (configs.TryGetValue("RetroArch", out var retroArch) &&
                EmulatorSettingsJson.Deserialize<RetroArchSettings>(retroArch) is { } retroArchV)
                RetroArch.CopyFrom(retroArchV);
            if (configs.TryGetValue("Rpcs3", out var rpcs3) &&
                EmulatorSettingsJson.Deserialize<Rpcs3Settings>(rpcs3) is { } rpcs3V) Rpcs3.CopyFrom(rpcs3V);
            if (configs.TryGetValue("SegaModel2", out var segaModel2) &&
                EmulatorSettingsJson.Deserialize<SegaModel2Settings>(segaModel2) is { } segaModel2V)
                SegaModel2.CopyFrom(segaModel2V);
            if (configs.TryGetValue("Stella", out var stella) &&
                EmulatorSettingsJson.Deserialize<StellaSettings>(stella) is { } stellaV) Stella.CopyFrom(stellaV);
            if (configs.TryGetValue("Supermodel", out var supermodel) &&
                EmulatorSettingsJson.Deserialize<SupermodelSettings>(supermodel) is { } supermodelV)
                Supermodel.CopyFrom(supermodelV);
            if (configs.TryGetValue("Xenia", out var xenia) &&
                EmulatorSettingsJson.Deserialize<XeniaSettings>(xenia) is { } xeniaV) Xenia.CopyFrom(xeniaV);
            if (configs.TryGetValue("Yumir", out var yumir) &&
                EmulatorSettingsJson.Deserialize<YumirSettings>(yumir) is { } yumirV) Yumir.CopyFrom(yumirV);
        }
        finally
        {
            _settingsLock.ExitWriteLock();
        }
    }

    /// <summary>Exports per-system play times for the SystemPlayTimes table.</summary>
    public List<SystemPlayTimeRecord> ExportSystemPlayTimes()
    {
        _settingsLock.EnterReadLock();
        try
        {
            return SystemPlayTimes
                .Select(static pt => new SystemPlayTimeRecord(pt.SystemName, pt.PlayTimeSeconds))
                .ToList();
        }
        finally
        {
            _settingsLock.ExitReadLock();
        }
    }

    /// <summary>Replaces per-system play times (e.g. after loading from the database).</summary>
    public void ImportSystemPlayTimes(IReadOnlyList<SystemPlayTimeRecord> playTimes)
    {
        ArgumentNullException.ThrowIfNull(playTimes);

        _settingsLock.EnterWriteLock();
        try
        {
            SystemPlayTimes.Clear();
            foreach (var pt in playTimes)
            {
                if (string.IsNullOrWhiteSpace(pt.SystemName))
                    continue;
                SystemPlayTimes.Add(new SystemPlayTime
                {
                    SystemName = pt.SystemName,
                    PlayTimeSeconds = pt.PlayTimeSeconds
                });
            }
        }
        finally
        {
            _settingsLock.ExitWriteLock();
        }
    }

    /// <summary>Loads all settings from the unified database (creates it when missing).</summary>
    public void LoadFromDatabase(string? dbPath = null)
    {
        UnifiedSettingsDatabase.EnsureCreated(dbPath);
        var app = UnifiedSettingsDatabase.LoadAppSettings(dbPath);
        var emulators = UnifiedSettingsDatabase.LoadEmulatorConfigs(dbPath);
        var playTimes = UnifiedSettingsDatabase.LoadSystemPlayTimes(dbPath);

        if (app.Count == 0 && emulators.Count == 0 && playTimes.Count == 0)
        {
            // Fresh database with no migrated content — persist the defaults so the
            // file is complete (mirrors SetDefaultsAndSave for the XML path).
            _settingsLock.EnterWriteLock();
            try
            {
                ResetToDefaultsLocked();
            }
            finally
            {
                _settingsLock.ExitWriteLock();
            }

            SaveToDatabase(dbPath);
            return;
        }

        if (app.Count > 0)
            ImportAppSettings(app);
        if (emulators.Count > 0)
            ImportEmulatorSettings(emulators);
        ImportSystemPlayTimes(playTimes);
    }

    /// <summary>Saves all settings to the unified database.</summary>
    public void SaveToDatabase(string? dbPath = null)
    {
        var app = ExportAppSettings();
        var emulators = ExportEmulatorSettings();
        var playTimes = ExportSystemPlayTimes();

        UnifiedSettingsDatabase.EnsureCreated(dbPath);
        UnifiedSettingsDatabase.SaveAppSettings(app, dbPath);
        UnifiedSettingsDatabase.SaveAllEmulatorConfigs(emulators, dbPath);
        UnifiedSettingsDatabase.SaveSystemPlayTimes(playTimes, dbPath);
    }

    /// <summary>Resets to defaults without taking the lock (callers must hold the write lock).</summary>
    private void ResetToDefaultsLocked()
    {
        CopyFrom(new SettingsManagerService(_configuration, _logger, _credentialProtector, _messageBox,
            UseUnifiedDatabase));
    }

    private void SetDefaultsAndSave()
    {
        ResetToDefaults();
        _ = SaveAsync();
    }

    /// <summary>
    ///     Updates the cumulative play time for the specified system.
    /// </summary>
    public void UpdateSystemPlayTime(string systemName, TimeSpan playTime)
    {
        if (string.IsNullOrWhiteSpace(systemName) || playTime == TimeSpan.Zero) return;

        _settingsLock.EnterWriteLock();
        try
        {
            var item = SystemPlayTimes.FirstOrDefault(s =>
                s.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                item = new SystemPlayTime { SystemName = systemName, PlayTimeSeconds = 0 };
                SystemPlayTimes.Add(item);
            }

            item.PlayTimeSeconds += (long)playTime.TotalSeconds;
        }
        finally
        {
            _settingsLock.ExitWriteLock();
        }
    }
}