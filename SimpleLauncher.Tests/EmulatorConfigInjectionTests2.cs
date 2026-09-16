using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Tests.TestHelpers;
using Tomlyn;
using Tomlyn.Model;
using Xunit;
using MameConfigurationService = SimpleLauncher.Core.Services.InjectEmulatorConfig.MameConfigurationService;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for emulator configuration injection across multiple emulators (Ares, Azahar, Cemu, Dolphin, Flycast, Mame,
///     Supermodel, SegaModel2, Yumir, Raine).
/// </summary>
public class EmulatorConfigInjectionTests2 : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly NoOpCredentialProtector _credentialProtector = new();
    private readonly ILogger _logErrors = new NoOpLogger();
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EmulatorConfigInjectionTests2" /> class, setting up a temporary test
    ///     directory and in-memory configuration.
    /// </summary>
    public EmulatorConfigInjectionTests2()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Urls:YouTubeSearch"] = "https://www.youtube.com/results?search_query=",
                ["Urls:IgdbSearch"] = "https://www.igdb.com/search?q="
            })
            .Build();

        ServiceProviderMock.Install();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_EmuInjectionTest2_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the temporary test directory and restores the original service provider.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        ServiceProviderMock.Restore();
        GC.SuppressFinalize(this);
    }

    private void CopySampleToEmuDir(string emulatorDirName, string sampleSubDir, string configFileName)
    {
        var emuDir = Path.Combine(_testDirectory, emulatorDirName);
        Directory.CreateDirectory(emuDir);

        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", sampleSubDir, configFileName);
        var destPath = Path.Combine(emuDir, configFileName);
        File.Copy(samplePath, destPath);
    }

    private static string FakeEmulatorExePath(string emuDir)
    {
        return Path.Combine(emuDir, "emulator.exe");
    }

    private SettingsManagerService CreateSettingsManager()
    {
        return new SettingsManagerService(_configuration, _logErrors, _credentialProtector);
    }

    /// <summary>
    ///     Verifies that Ares emulator settings are correctly injected into the settings.bml configuration file.
    /// </summary>
    [Fact]
    public void AresInjectsSettingsCorrectly()
    {
        CopySampleToEmuDir("Ares", "Ares", "settings.bml");

        var settings = CreateSettingsManager();
        settings.Ares.VideoDriver = "OpenGL 3.2";
        settings.Ares.Exclusive = true;
        settings.Ares.Shader = "CRT";
        settings.Ares.Multiplier = 4;
        settings.Ares.AspectCorrection = "Stretch";
        settings.Ares.Mute = true;
        settings.Ares.Volume = 0.5;
        settings.Ares.FastBoot = true;
        settings.Ares.Rewind = true;
        settings.Ares.RunAhead = false;
        settings.Ares.AutoSaveMemory = false;

        var emuDir = Path.Combine(_testDirectory, "Ares");
        AresConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "settings.bml");
        var content = File.ReadAllText(configPath);

        Assert.Contains("Driver: OpenGL 3.2", content, StringComparison.Ordinal);
        Assert.Contains("Exclusive: true", content, StringComparison.Ordinal);
        Assert.Contains("Shader: CRT", content, StringComparison.Ordinal);
        Assert.Contains("Multiplier: 4", content, StringComparison.Ordinal);
        Assert.Contains("Output: Stretch", content, StringComparison.Ordinal);
        Assert.Contains("Mute: true", content, StringComparison.Ordinal);
        Assert.Contains("Volume: 0.5", content, StringComparison.Ordinal);
        Assert.Contains("Fast: true", content, StringComparison.Ordinal);
        Assert.Contains("Rewind: true", content, StringComparison.Ordinal);
        Assert.Contains("RunAhead: false", content, StringComparison.Ordinal);
        Assert.Contains("AutoSaveMemory: false", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that Azahar emulator settings are correctly injected into the qt-config.ini configuration file.
    /// </summary>
    [Fact]
    public void AzaharInjectsSettingsCorrectly()
    {
        CopySampleToEmuDir("Azahar", "Azahar", "qt-config.ini");

        var settings = CreateSettingsManager();
        settings.Azahar.GraphicsApi = 1;
        settings.Azahar.ResolutionFactor = 2;
        settings.Azahar.UseVsync = true;
        settings.Azahar.AsyncShaderCompilation = false;
        settings.Azahar.Fullscreen = true;
        settings.Azahar.Volume = 75;
        settings.Azahar.EnableAudioStretching = true;
        settings.Azahar.IsNew3Ds = false;
        settings.Azahar.LayoutOption = 1;

        var emuDir = Path.Combine(_testDirectory, "Azahar");
        AzaharConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "qt-config.ini");
        var lines = File.ReadAllLines(configPath).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("1", sectionValues[("Renderer", "graphics_api")]);
        Assert.Equal("2", sectionValues[("Renderer", "resolution_factor")]);
        Assert.Equal("true", sectionValues[("Renderer", "use_vsync_new")]);
        Assert.Equal("false", sectionValues[("Renderer", "async_shader_compilation")]);
        Assert.Equal("true", sectionValues[("UI", "fullscreen")]);
        Assert.Equal("0.75", sectionValues[("Audio", "volume")]);
        Assert.Equal("true", sectionValues[("Audio", "enable_audio_stretching")]);
        Assert.Equal("false", sectionValues[("System", "is_new_3ds")]);
        Assert.Equal("1", sectionValues[("Layout", "layout_option")]);
    }

    /// <summary>
    ///     Verifies that Azahar injection sets the default key to false for injected settings.
    /// </summary>
    [Fact]
    public void AzaharInjectsDefaultKeysAsFalse()
    {
        CopySampleToEmuDir("Azahar", "Azahar", "qt-config.ini");

        var settings = CreateSettingsManager();
        settings.Azahar.Fullscreen = true;

        var emuDir = Path.Combine(_testDirectory, "Azahar");
        AzaharConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "qt-config.ini");
        var lines = File.ReadAllLines(configPath).ToList();
        var sectionValues = ParseIniSections(lines);

        // The \default key should be set to "false" for injected settings
        Assert.Equal("false", sectionValues[("UI", "fullscreen\\default")]);
    }

    /// <summary>
    ///     Verifies that an I/O failure while writing an existing qt-config.ini (simulated by another
    ///     process holding the file open) surfaces an <see cref="AzaharPermissionException" /> instead
    ///     of a raw IOException, so the launcher can still start the game (bug 66928).
    /// </summary>
    [Fact]
    public void AzaharLockedConfigFileThrowsPermissionException()
    {
        var emuDir = Path.Combine(_testDirectory, "AzaharLocked");
        Directory.CreateDirectory(emuDir);
        var configPath = Path.Combine(emuDir, "qt-config.ini");
        File.WriteAllText(configPath, "[UI]\nfullscreen=true\n");

        var settings = CreateSettingsManager();

        using var lockStream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<AzaharPermissionException>(() =>
            AzaharConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger));
    }

    /// <summary>
    ///     Verifies that Cemu emulator settings are correctly injected into the settings.xml configuration file.
    /// </summary>
    [Fact]
    public void CemuInjectsXmlSettingsCorrectly()
    {
        CopySampleToEmuDir("Cemu", "Cemu", "settings.xml");

        var settings = CreateSettingsManager();
        settings.Cemu.Fullscreen = true;
        settings.Cemu.DiscordPresence = false;
        settings.Cemu.ConsoleLanguage = 2;
        settings.Cemu.GraphicApi = 1;
        settings.Cemu.Vsync = 1;
        settings.Cemu.AsyncCompile = true;
        settings.Cemu.TvVolume = 80;

        var emuDir = Path.Combine(_testDirectory, "Cemu");
        CemuConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "settings.xml");
        var doc = XDocument.Load(configPath);
        var content = doc.Element("content");

        Assert.NotNull(content);
        Assert.Equal("true", content.Element("fullscreen")?.Value);
        Assert.Equal("false", content.Element("use_discord_presence")?.Value);
        Assert.Equal("2", content.Element("console_language")?.Value);

        var graphic = content.Element("Graphic");
        Assert.NotNull(graphic);
        Assert.Equal("1", graphic.Element("api")?.Value);
        Assert.Equal("1", graphic.Element("VSync")?.Value);
        Assert.Equal("true", graphic.Element("AsyncCompile")?.Value);

        var audio = content.Element("Audio");
        Assert.NotNull(audio);
        Assert.Equal("80", audio.Element("TVVolume")?.Value);
    }

    /// <summary>
    ///     Verifies that Dolphin emulator settings are correctly injected into the Dolphin.ini configuration file.
    /// </summary>
    [Fact]
    public void DolphinInjectsSettingsCorrectly()
    {
        CopySampleToEmuDir("Dolphin", "Dolphin", "Dolphin.ini");

        var settings = CreateSettingsManager();
        settings.Dolphin.GfxBackend = "D3D12";
        settings.Dolphin.WiimoteContinuousScanning = false;
        settings.Dolphin.WiimoteEnableSpeaker = false;
        settings.Dolphin.DspThread = false;

        var emuDir = Path.Combine(_testDirectory, "Dolphin");

        // Create portable.txt so Dolphin uses the local config
        File.WriteAllText(Path.Combine(emuDir, "portable.txt"), "");

        DolphinConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "User", "Config", "Dolphin.ini");
        Assert.True(File.Exists(configPath), $"Config file should exist at {configPath}");

        var lines = File.ReadAllLines(configPath).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("D3D12", sectionValues[("Core", "GFXBackend")]);
        Assert.Equal("False", sectionValues[("Core", "WiimoteContinuousScanning")]);
        Assert.Equal("False", sectionValues[("Core", "WiimoteEnableSpeaker")]);
        Assert.Equal("False", sectionValues[("DSP", "DSPThread")]);
    }

    /// <summary>
    ///     Verifies that Flycast emulator settings are correctly injected into the emu.cfg configuration file.
    /// </summary>
    [Fact]
    public void FlycastInjectsSettingsCorrectly()
    {
        CopySampleToEmuDir("Flycast", "Flycast", "emu.cfg");

        var settings = CreateSettingsManager();
        settings.Flycast.Fullscreen = true;
        settings.Flycast.Width = 1920;
        settings.Flycast.Height = 1080;
        settings.Flycast.Maximized = true;

        var emuDir = Path.Combine(_testDirectory, "Flycast");
        FlycastConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "emu.cfg");
        var lines = File.ReadAllLines(configPath).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("yes", sectionValues[("window", "fullscreen")]);
        Assert.Equal("1920", sectionValues[("window", "width")]);
        Assert.Equal("1080", sectionValues[("window", "height")]);
        Assert.Equal("yes", sectionValues[("window", "maximized")]);
    }

    /// <summary>
    ///     Verifies that Flycast disabled options use "no" values in the configuration file.
    /// </summary>
    [Fact]
    public void FlycastDisabledOptionsUsesNoValues()
    {
        CopySampleToEmuDir("Flycast", "Flycast", "emu.cfg");

        var settings = CreateSettingsManager();
        settings.Flycast.Fullscreen = false;
        settings.Flycast.Maximized = false;

        var emuDir = Path.Combine(_testDirectory, "Flycast");
        FlycastConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "emu.cfg");
        var lines = File.ReadAllLines(configPath).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("no", sectionValues[("window", "fullscreen")]);
        Assert.Equal("no", sectionValues[("window", "maximized")]);
    }

    /// <summary>
    ///     Verifies that MAME emulator settings are correctly injected into the mame.ini configuration file.
    /// </summary>
    [Fact]
    public void MameInjectsSettingsCorrectly()
    {
        CopySampleToEmuDir("Mame", "MAME", "mame.ini");

        var settings = CreateSettingsManager();
        settings.Mame.Video = "d3d";
        settings.Mame.Window = false;
        settings.Mame.Maximize = true;
        settings.Mame.KeepAspect = true;
        settings.Mame.SkipGameInfo = true;
        settings.Mame.Autosave = true;
        settings.Mame.ConfirmQuit = false;
        settings.Mame.Joystick = true;
        settings.Mame.Autoframeskip = false;
        settings.Mame.BgfxBackend = "vulkan";
        settings.Mame.BgfxScreenChains = "crt-geom";
        settings.Mame.Filter = true;
        settings.Mame.Cheat = true;
        settings.Mame.Rewind = true;
        settings.Mame.NvramSave = false;

        var emuDir = Path.Combine(_testDirectory, "Mame");
        MameConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "mame.ini");
        var content = File.ReadAllText(configPath);

        Assert.Contains("video", content, StringComparison.Ordinal);
        Assert.Contains("d3d", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that mame.ini keeps existing rompath entries whose directories are
    ///     temporarily unavailable (offline network share, unplugged drive) instead of
    ///     dropping them on the next settings injection.
    /// </summary>
    [Fact]
    public void MameKeepsUnavailableRomPaths()
    {
        CopySampleToEmuDir("Mame", "MAME", "mame.ini");

        var emuDir = Path.Combine(_testDirectory, "Mame");
        var configPath = Path.Combine(emuDir, "mame.ini");

        var lines = File.ReadAllLines(configPath).ToList();
        var romPathIndex = lines.FindIndex(static l =>
            l.TrimStart().StartsWith("rompath", StringComparison.OrdinalIgnoreCase));
        Assert.True(romPathIndex >= 0, "The MAME sample must contain a rompath line.");
        lines[romPathIndex] = @"rompath \\offline-server\roms;Z:\missing-roms";
        File.WriteAllLines(configPath, lines);

        var settings = CreateSettingsManager();
        MameConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var content = File.ReadAllText(configPath);
        Assert.Contains(@"\\offline-server\roms", content, StringComparison.Ordinal);
        Assert.Contains(@"Z:\missing-roms", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that Supermodel emulator settings are correctly injected into the Supermodel.ini configuration file.
    /// </summary>
    [Fact]
    public void SupermodelInjectsSettingsCorrectly()
    {
        CopySampleToEmuDir("Supermodel", "Supermodel", "Supermodel.ini");

        var settings = CreateSettingsManager();
        settings.Supermodel.New3DEngine = true;
        settings.Supermodel.QuadRendering = true;
        settings.Supermodel.Fullscreen = true;
        settings.Supermodel.ResX = 1920;
        settings.Supermodel.ResY = 1080;
        settings.Supermodel.WideScreen = true;
        settings.Supermodel.Stretch = false;
        settings.Supermodel.Vsync = true;
        settings.Supermodel.Throttle = true;
        settings.Supermodel.MusicVolume = 100;
        settings.Supermodel.SoundVolume = 100;
        settings.Supermodel.InputSystem = "dinput";
        settings.Supermodel.MultiThreaded = true;
        settings.Supermodel.PowerPcFrequency = 50;

        var emuDir = Path.Combine(_testDirectory, "Supermodel");
        SupermodelConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "Supermodel.ini");
        Assert.True(File.Exists(configPath), $"Config file should exist at {configPath}");

        var content = File.ReadAllText(configPath);

        Assert.Contains("New3DEngine = 1", content, StringComparison.Ordinal);
        Assert.Contains("QuadRendering = 1", content, StringComparison.Ordinal);
        Assert.Contains("FullScreen = 1", content, StringComparison.Ordinal);
        Assert.Contains("XResolution = 1920", content, StringComparison.Ordinal);
        Assert.Contains("YResolution = 1080", content, StringComparison.Ordinal);
        Assert.Contains("WideScreen = 1", content, StringComparison.Ordinal);
        Assert.Contains("Stretch = 0", content, StringComparison.Ordinal);
        Assert.Contains("VSync = 1", content, StringComparison.Ordinal);
        Assert.Contains("Throttle = 1", content, StringComparison.Ordinal);
        Assert.Contains("MusicVolume = 100", content, StringComparison.Ordinal);
        Assert.Contains("SoundVolume = 100", content, StringComparison.Ordinal);
        Assert.Contains("InputSystem = dinput", content, StringComparison.Ordinal);
        Assert.Contains("MultiThreaded = 1", content, StringComparison.Ordinal);
        Assert.Contains("PowerPCFrequency = 50", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that Supermodel defaults to xinput when an invalid input system value is provided.
    /// </summary>
    [Fact]
    public void SupermodelInvalidInputSystemDefaultsToXinput()
    {
        CopySampleToEmuDir("Supermodel", "Supermodel", "Supermodel.ini");

        var settings = CreateSettingsManager();
        settings.Supermodel.InputSystem = "invalid_value";

        var emuDir = Path.Combine(_testDirectory, "Supermodel");
        SupermodelConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "Supermodel.ini");
        var content = File.ReadAllText(configPath);

        Assert.Contains("InputSystem = xinput", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that Sega Model 2 emulator settings are correctly injected into the EMULATOR.INI configuration file.
    /// </summary>
    [Fact]
    public void SegaModel2InjectsSettingsCorrectly()
    {
        CopySampleToEmuDir("SegaModel2", "SEGA Model 2", "EMULATOR.INI");

        var settings = CreateSettingsManager();
        settings.SegaModel2.ResX = 1920;
        settings.SegaModel2.ResY = 1080;
        settings.SegaModel2.WideScreen = 1;
        settings.SegaModel2.Bilinear = true;
        settings.SegaModel2.Trilinear = true;
        settings.SegaModel2.FilterTilemaps = true;
        settings.SegaModel2.DrawCross = false;
        settings.SegaModel2.Fsaa = 4;
        settings.SegaModel2.XInput = true;
        settings.SegaModel2.EnableFf = true;
        settings.SegaModel2.HoldGears = true;
        settings.SegaModel2.UseRawInput = true;

        var emuDir = Path.Combine(_testDirectory, "SegaModel2");
        SegaModel2ConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "EMULATOR.INI");
        var lines = File.ReadAllLines(configPath).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("1920", sectionValues[("Renderer", "FullScreenWidth")]);
        Assert.Equal("1080", sectionValues[("Renderer", "FullScreenHeight")]);
        Assert.Equal("1", sectionValues[("Renderer", "WideScreenWindow")]);
        Assert.Equal("1", sectionValues[("Renderer", "Bilinear")]);
        Assert.Equal("1", sectionValues[("Renderer", "Trilinear")]);
        Assert.Equal("1", sectionValues[("Renderer", "FilterTilemaps")]);
        Assert.Equal("0", sectionValues[("Renderer", "DrawCross")]);
        Assert.Equal("4", sectionValues[("Renderer", "FSAA")]);
        Assert.Equal("1", sectionValues[("Input", "XInput")]);
        Assert.Equal("1", sectionValues[("Input", "EnableFF")]);
        Assert.Equal("1", sectionValues[("Input", "HoldGears")]);
        Assert.Equal("1", sectionValues[("Input", "UseRawInput")]);
    }

    /// <summary>
    ///     Verifies that Yumir emulator settings are correctly injected into the Ymir.toml configuration file.
    /// </summary>
    [Fact]
    public void YumirInjectsTomlSettingsCorrectly()
    {
        CopySampleToEmuDir("Yumir", "Yumir", "Ymir.toml");

        var settings = CreateSettingsManager();
        settings.Yumir.Fullscreen = true;
        settings.Yumir.ForceAspectRatio = true;
        settings.Yumir.ForcedAspect = 1.78;
        settings.Yumir.ReduceLatency = true;
        settings.Yumir.Volume = 0.5;
        settings.Yumir.Mute = true;
        settings.Yumir.VideoStandard = "NTSC";
        settings.Yumir.AutoDetectRegion = false;
        settings.Yumir.PauseWhenUnfocused = true;

        var emuDir = Path.Combine(_testDirectory, "Yumir");
        YumirConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "Ymir.toml");
        var tomlContent = File.ReadAllText(configPath);
        var model = TomlSerializer.Deserialize<TomlTable>(tomlContent) ?? new TomlTable();

        var video = (TomlTable)model["Video"];
        Assert.True((bool)video["FullScreen"]);
        Assert.True((bool)video["ForceAspectRatio"]);
        Assert.True((bool)video["ReduceLatency"]);

        var audio = (TomlTable)model["Audio"];
        Assert.True((bool)audio["Mute"]);

        var system = (TomlTable)model["System"];
        Assert.Equal("NTSC", (string)system["VideoStandard"]);
        Assert.False((bool)system["AutoDetectRegion"]);

        var general = (TomlTable)model["General"];
        Assert.True((bool)general["PauseWhenUnfocused"]);
    }

    /// <summary>
    ///     Verifies that Raine emulator settings are correctly injected into the raine32_sdl.cfg configuration file.
    /// </summary>
    [Fact]
    public void RaineInjectsSettingsCorrectly()
    {
        CopySampleToEmuDir("Raine", "Raine", "raine32_sdl.cfg");

        var settings = CreateSettingsManager();
        settings.Raine.Fullscreen = true;
        settings.Raine.ResX = 1920;
        settings.Raine.ResY = 1080;
        settings.Raine.FixAspectRatio = false;
        settings.Raine.Vsync = true;
        settings.Raine.SoundDriver = "SDL";
        settings.Raine.SampleRate = 48000;
        settings.Raine.FrameSkip = 1;
        settings.Raine.ShowFps = true;

        var emuDir = Path.Combine(_testDirectory, "Raine");
        RaineConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "config", "raine32_sdl.cfg");
        Assert.True(File.Exists(configPath), $"Config file should exist at {configPath}");

        var lines = File.ReadAllLines(configPath).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("1", sectionValues[("Display", "fullscreen")]);
        Assert.Equal("1920", sectionValues[("Display", "screen_x")]);
        Assert.Equal("1080", sectionValues[("Display", "screen_y")]);
        Assert.Equal("0", sectionValues[("Display", "fix_aspect_ratio")]);
        Assert.Equal("2", sectionValues[("Display", "ogl_dbuf")]);
        Assert.Equal("SDL", sectionValues[("Sound", "driver")]);
        Assert.Equal("48000", sectionValues[("Sound", "sample_rate")]);
        Assert.Equal("1", sectionValues[("General", "frame_skip")]);
        Assert.Equal("1", sectionValues[("General", "ShowFPS")]);
    }

    // --- Helpers ---

    private static Dictionary<(string Section, string Key), string> ParseIniSections(List<string> lines)
    {
        var result = new Dictionary<(string, string), string>();
        var currentSection = "";

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1];
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2) result[(currentSection, parts[0].Trim())] = parts[1].Trim();
        }

        return result;
    }
}