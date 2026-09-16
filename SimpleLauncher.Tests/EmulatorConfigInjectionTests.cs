using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Tests.TestHelpers;
using Tomlyn;
using Tomlyn.Model;
using Xunit;
using YamlDotNet.Serialization;
using BlastemConfigurationService = SimpleLauncher.Core.Services.InjectEmulatorConfig.BlastemConfigurationService;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for emulator configuration injection into various emulator config file formats (INI, JSON, TOML, YAML, flat
///     key-value).
/// </summary>
[SuppressMessage("ReSharper", "NullableWarningSuppressionIsUsed")]
public class EmulatorConfigInjectionTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly NoOpCredentialProtector _credentialProtector = new();
    private readonly ILogger _logErrors = new NoOpLogger();
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of <see cref="EmulatorConfigInjectionTests" /> with in-memory configuration and a
    ///     temporary test directory.
    /// </summary>
    public EmulatorConfigInjectionTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Urls:YouTubeSearch"] = "https://www.youtube.com/results?search_query=",
                ["Urls:IgdbSearch"] = "https://www.igdb.com/search?q="
            })
            .Build();

        ServiceProviderMock.Install();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_EmuInjectionTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the test directory and restores the service provider mock.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    ///     Copies a sample config file from the test samples directory into a temporary emulator directory.
    /// </summary>
    private void CopySampleToEmuDir(string emulatorDirName, string sampleSubDir, string configFileName)
    {
        var emuDir = Path.Combine(_testDirectory, emulatorDirName);
        Directory.CreateDirectory(emuDir);

        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", sampleSubDir, configFileName);
        var destPath = Path.Combine(emuDir, configFileName);
        File.Copy(samplePath, destPath);
    }

    /// <summary>
    ///     Returns a fake emulator executable path within the specified emulator directory.
    /// </summary>
    private static string FakeEmulatorExePath(string emuDir)
    {
        return Path.Combine(emuDir, "emulator.exe");
    }

    /// <summary>
    ///     Creates a new SettingsManagerService instance with test configuration.
    /// </summary>
    private SettingsManagerService CreateSettingsManager()
    {
        return new SettingsManagerService(_configuration, _logErrors, _credentialProtector);
    }

    /// <summary>
    ///     Verifies that DuckStation settings are correctly injected into an INI config file.
    /// </summary>
    [Fact]
    public void DuckStationInjectsSettingsCorrectly()
    {
        var emuDir = Path.Combine(_testDirectory, "DuckStation");
        Directory.CreateDirectory(emuDir);
        File.WriteAllText(Path.Combine(emuDir, "portable.txt"), string.Empty);
        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "DuckStation", "settings.ini");
        var destPath = Path.Combine(emuDir, "settings.ini");
        File.Copy(samplePath, destPath);

        var settings = CreateSettingsManager();
        settings.DuckStation.StartFullscreen = true;
        settings.DuckStation.PauseOnFocusLoss = false;
        settings.DuckStation.SaveStateOnExit = false;
        settings.DuckStation.Renderer = "Vulkan";
        settings.DuckStation.ResolutionScale = 4;
        settings.DuckStation.TextureFilter = "Bilinear";
        settings.DuckStation.WidescreenHack = true;
        settings.DuckStation.PgxpEnable = false;
        settings.DuckStation.AspectRatio = "4:3";
        settings.DuckStation.Vsync = true;
        settings.DuckStation.OutputVolume = 50;
        settings.DuckStation.OutputMuted = true;

        DuckStationConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var lines = File.ReadAllLines(destPath).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("true", sectionValues[("Main", "StartFullscreen")]);
        Assert.Equal("false", sectionValues[("Main", "PauseOnFocusLoss")]);
        Assert.Equal("false", sectionValues[("Main", "SaveStateOnExit")]);
        Assert.Equal("Vulkan", sectionValues[("GPU", "Renderer")]);
        Assert.Equal("4", sectionValues[("GPU", "ResolutionScale")]);
        Assert.Equal("Bilinear", sectionValues[("GPU", "TextureFilter")]);
        Assert.Equal("true", sectionValues[("GPU", "WidescreenHack")]);
        Assert.Equal("false", sectionValues[("GPU", "PGXPEnable")]);
        Assert.Equal("4:3", sectionValues[("Display", "AspectRatio")]);
        Assert.Equal("true", sectionValues[("Display", "VSync")]);
        Assert.Equal("50", sectionValues[("Audio", "OutputVolume")]);
        Assert.Equal("true", sectionValues[("Audio", "OutputMuted")]);
    }

    /// <summary>
    ///     Verifies that PCSX2 settings are correctly injected into an INI config file.
    /// </summary>
    [Fact]
    public void Pcsx2InjectsSettingsCorrectly()
    {
        var emuDir = Path.Combine(_testDirectory, "PCSX2");
        Directory.CreateDirectory(emuDir);
        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "PCSX2", "PCSX2.ini");
        var destPath = Path.Combine(emuDir, "PCSX2.ini");
        File.Copy(samplePath, destPath);

        var settings = CreateSettingsManager();
        settings.Pcsx2.StartFullscreen = false;
        settings.Pcsx2.EnableCheats = true;
        settings.Pcsx2.EnableWidescreenPatches = true;
        settings.Pcsx2.Renderer = 11; // Software
        settings.Pcsx2.UpscaleMultiplier = 3;
        settings.Pcsx2.AspectRatio = "4:3";
        settings.Pcsx2.Vsync = true;
        settings.Pcsx2.Volume = 75;
        settings.Pcsx2.AchievementsEnabled = true;
        settings.Pcsx2.AchievementsHardcore = false;

        Pcsx2ConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var lines = File.ReadAllLines(destPath).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("false", sectionValues[("UI", "StartFullscreen")]);
        Assert.Equal("true", sectionValues[("EmuCore", "EnableCheats")]);
        Assert.Equal("true", sectionValues[("EmuCore", "EnableWideScreenPatches")]);
        Assert.Equal("11", sectionValues[("EmuCore/GS", "Renderer")]);
        Assert.Equal("3", sectionValues[("EmuCore/GS", "upscale_multiplier")]);
        Assert.Equal("4:3", sectionValues[("EmuCore/GS", "AspectRatio")]);
        Assert.Equal("true", sectionValues[("EmuCore/GS", "VsyncEnable")]);
        Assert.Equal("75", sectionValues[("SPU2/Mixing", "FinalVolume")]);
        Assert.Equal("true", sectionValues[("Achievements", "Enabled")]);
        Assert.Equal("false", sectionValues[("Achievements", "Hardcore")]);
    }

    /// <summary>
    ///     Verifies that when PCSX2 runs in portable mode (portable.ini marker present) and no config exists,
    ///     the PCSX2.ini is created from the sample inside the emulator's 'inis' subfolder.
    /// </summary>
    [Fact]
    public void Pcsx2PortableMarkerCreatesConfigInEmulatorInisFolder()
    {
        var emuDir = Path.Combine(_testDirectory, "PCSX2Portable");
        Directory.CreateDirectory(emuDir);
        File.WriteAllText(Path.Combine(emuDir, "portable.ini"), string.Empty);

        var settings = CreateSettingsManager();
        settings.Pcsx2.EnableCheats = true;

        Pcsx2ConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var expectedConfigPath = Path.Combine(emuDir, "inis", "PCSX2.ini");
        Assert.True(File.Exists(expectedConfigPath));

        var lines = File.ReadAllLines(expectedConfigPath).ToList();
        var sectionValues = ParseIniSections(lines);
        Assert.Equal("true", sectionValues[("EmuCore", "EnableCheats")]);
    }

    /// <summary>
    ///     Verifies that when the PCSX2 config folder cannot be created (e.g. a stale or cloud-only
    ///     OneDrive-redirected Documents folder, simulated here by a file occupying the 'inis' path),
    ///     injection surfaces a <see cref="Pcsx2PermissionException" /> instead of a raw IOException,
    ///     so the launcher can still start the game with default settings (bug 66928).
    /// </summary>
    [Fact]
    public void Pcsx2UnavailableConfigDirectoryThrowsPermissionException()
    {
        var emuDir = Path.Combine(_testDirectory, "PCSX2Blocked");
        Directory.CreateDirectory(emuDir);
        File.WriteAllText(Path.Combine(emuDir, "portable.ini"), string.Empty);
        File.WriteAllText(Path.Combine(emuDir, "inis"), "not a directory");

        var settings = CreateSettingsManager();

        Assert.Throws<Pcsx2PermissionException>(() =>
            Pcsx2ConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger));
    }

    /// <summary>
    ///     Verifies that an I/O failure while writing an existing PCSX2.ini (simulated by another
    ///     process holding the file open) surfaces a <see cref="Pcsx2PermissionException" /> instead
    ///     of a raw IOException, so the launcher can still start the game (bug 66928).
    /// </summary>
    [Fact]
    public void Pcsx2LockedConfigFileThrowsPermissionException()
    {
        var emuDir = Path.Combine(_testDirectory, "PCSX2Locked");
        Directory.CreateDirectory(emuDir);
        var configPath = Path.Combine(emuDir, "PCSX2.ini");
        File.WriteAllText(configPath, "[UI]\nStartFullscreen = false\n");

        var settings = CreateSettingsManager();

        using var lockStream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<Pcsx2PermissionException>(() =>
            Pcsx2ConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger));
    }

    /// <summary>
    ///     Verifies that Mesen settings are correctly injected into a JSON config file.
    /// </summary>
    [Fact]
    public void MesenInjectsJsonSettingsCorrectly()
    {
        CopySampleToEmuDir("Mesen", "Mesen", "settings.json");

        var settings = CreateSettingsManager();
        settings.Mesen.Fullscreen = true;
        settings.Mesen.AspectRatio = "16:9";
        settings.Mesen.Vsync = false;
        settings.Mesen.Bilinear = false;
        settings.Mesen.VideoFilter = "CRT";
        settings.Mesen.EnableAudio = false;
        settings.Mesen.MasterVolume = 50;
        settings.Mesen.Rewind = true;
        settings.Mesen.RunAhead = 2;
        settings.Mesen.PauseInBackground = true;

        var emuDir = Path.Combine(_testDirectory, "Mesen");
        MesenConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "settings.json");
        var root = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();

        var video = root["Video"]!.AsObject();
        Assert.True((bool)video["UseExclusiveFullscreen"]!);
        Assert.Equal("Widescreen", (string)video["AspectRatio"]!);
        Assert.False((bool)video["VerticalSync"]!);
        Assert.False((bool)video["UseBilinearInterpolation"]!);
        Assert.Equal("CRT", (string)video["VideoFilter"]!);

        var audio = root["Audio"]!.AsObject();
        Assert.False((bool)audio["EnableAudio"]!);
        Assert.Equal(50, (int)audio["MasterVolume"]!);

        var preferences = root["Preferences"]!.AsObject();
        Assert.True((bool)preferences["EnableRewind"]!);
        Assert.True((bool)preferences["PauseWhenInBackground"]!);

        var emulation = root["Emulation"]!.AsObject();
        Assert.Equal(2, (int)emulation["RunAheadFrames"]!);
    }

    /// <summary>
    ///     Verifies that Xenia settings are correctly injected into a TOML config file.
    /// </summary>
    [Fact]
    public void XeniaInjectsTomlSettingsCorrectly()
    {
        CopySampleToEmuDir("Xenia", "Xenia", "xenia.config.toml");

        var settings = CreateSettingsManager();
        settings.Xenia.Apu = "xaudio2";
        settings.Xenia.Mute = true;
        settings.Xenia.Gpu = "vulkan";
        settings.Xenia.Vsync = false;
        settings.Xenia.ResScaleX = 2;
        settings.Xenia.ResScaleY = 2;
        settings.Xenia.Fullscreen = true;
        settings.Xenia.Aa = "fxaa";
        settings.Xenia.Scaling = "unscaled";
        settings.Xenia.Hid = "winkey";
        settings.Xenia.Vibration = false;
        settings.Xenia.DiscordPresence = false;
        settings.Xenia.ApplyPatches = true;
        settings.Xenia.ReadbackResolve = "fast";
        settings.Xenia.GammaSrgb = true;
        settings.Xenia.UserLanguage = 10;
        settings.Xenia.MountCache = false;

        var emuDir = Path.Combine(_testDirectory, "Xenia");
        XeniaConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "xenia.config.toml");
        var tomlContent = File.ReadAllText(configPath);
        var model = TomlSerializer.Deserialize<TomlTable>(tomlContent) ?? new TomlTable();

        var apu = (TomlTable)model["APU"];
        Assert.Equal("xaudio2", (string)apu["apu"]);
        Assert.True((bool)apu["mute"]);

        var gpu = (TomlTable)model["GPU"];
        Assert.Equal("vulkan", (string)gpu["gpu"]);
        Assert.False((bool)gpu["vsync"]);
        Assert.Equal(2L, (long)gpu["draw_resolution_scale_x"]);
        Assert.Equal(2L, (long)gpu["draw_resolution_scale_y"]);
        Assert.Equal("fast", (string)gpu["readback_resolve"]);
        Assert.True((bool)gpu["gamma_render_target_as_srgb"]);

        var display = (TomlTable)model["Display"];
        Assert.True((bool)display["fullscreen"]);
        Assert.Equal("fxaa", (string)display["postprocess_antialiasing"]);
        Assert.Equal("unscaled", (string)display["postprocess_scaling_and_sharpening"]);

        var hid = (TomlTable)model["HID"];
        Assert.Equal("winkey", (string)hid["hid"]);
        Assert.False((bool)hid["vibration"]);

        var general = (TomlTable)model["General"];
        Assert.False((bool)general["discord"]);
        Assert.True((bool)general["apply_patches"]);

        var storage = (TomlTable)model["Storage"];
        Assert.False((bool)storage["mount_cache"]);

        var xconfig = (TomlTable)model["XConfig"];
        Assert.Equal(10L, (long)xconfig["user_language"]);
    }

    /// <summary>
    ///     Verifies that RPCS3 settings are correctly injected into a YAML config file.
    /// </summary>
    [Fact]
    public void Rpcs3InjectsYamlSettingsCorrectly()
    {
        CopySampleToEmuDir("RPCS3", "RPCS3", "config.yml");

        var settings = CreateSettingsManager();
        settings.Rpcs3.PpuDecoder = "Interpreter (fast)";
        settings.Rpcs3.SpuDecoder = "Interpreter (precise)";
        settings.Rpcs3.Renderer = "OpenGL";
        settings.Rpcs3.Resolution = "1920x1080";
        settings.Rpcs3.AspectRatio = "4:3";
        settings.Rpcs3.Vsync = true;
        settings.Rpcs3.ResolutionScale = 200;
        settings.Rpcs3.AnisotropicFilter = 16;
        settings.Rpcs3.AudioRenderer = "XAudio2";
        settings.Rpcs3.AudioBuffering = false;
        settings.Rpcs3.StartFullscreen = true;

        var emuDir = Path.Combine(_testDirectory, "RPCS3");
        Rpcs3ConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "config.yml");
        var deserializer = new DeserializerBuilder().Build();
        var yamlObject = deserializer.Deserialize<Dictionary<object, object>>(File.ReadAllText(configPath));

        var core = (Dictionary<object, object>)yamlObject["Core"];
        Assert.Equal("Interpreter (fast)", core["PPU Decoder"].ToString());
        Assert.Equal("Interpreter (precise)", core["SPU Decoder"].ToString());

        var video = (Dictionary<object, object>)yamlObject["Video"];
        Assert.Equal("OpenGL", video["Renderer"].ToString());
        Assert.Equal("1920x1080", video["Resolution"].ToString());
        Assert.Equal("4:3", video["Aspect ratio"].ToString());
        Assert.Equal("true", video["VSync"].ToString());
        Assert.Equal("200", video["Resolution Scale"].ToString());
        Assert.Equal("16", video["Anisotropic Filter Override"].ToString());

        var audio = (Dictionary<object, object>)yamlObject["Audio"];
        Assert.Equal("XAudio2", audio["Renderer"].ToString());
        Assert.Equal("false", audio["Enable Buffering"].ToString());

        var misc = (Dictionary<object, object>)yamlObject["Miscellaneous"];
        Assert.Equal("true", misc["Start games in fullscreen mode"].ToString());
    }

    /// <summary>
    ///     Verifies that Redream settings are correctly injected into a flat key-value config file.
    /// </summary>
    [Fact]
    public void RedreamInjectsFlatKeyValueCorrectly()
    {
        CopySampleToEmuDir("Redream", "Redream", "redream.cfg");

        var settings = CreateSettingsManager();
        settings.Redream.Cable = "RGB";
        settings.Redream.Broadcast = "PAL";
        settings.Redream.Language = "pt";
        settings.Redream.Region = "europe";
        settings.Redream.Vsync = false;
        settings.Redream.Frameskip = false;
        settings.Redream.Aspect = "16:9";
        settings.Redream.Res = 4;
        settings.Redream.Renderer = "opengl";
        settings.Redream.Volume = 75;
        settings.Redream.Latency = 16;
        settings.Redream.Framerate = true;

        var emuDir = Path.Combine(_testDirectory, "Redream");
        RedreamConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var lines = File.ReadAllLines(Path.Combine(emuDir, "redream.cfg"));
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2) values[parts[0].Trim()] = parts[1].Trim();
        }

        Assert.Equal("RGB", values["cable"]);
        Assert.Equal("PAL", values["broadcast"]);
        Assert.Equal("pt", values["language"]);
        Assert.Equal("europe", values["region"]);
        Assert.Equal("0", values["vsync"]);
        Assert.Equal("0", values["frameskip"]);
        Assert.Equal("16:9", values["aspect"]);
        Assert.Equal("4", values["res"]);
        Assert.Equal("opengl", values["renderer"]);
        Assert.Equal("75", values["volume"]);
        Assert.Equal("16", values["latency"]);
        Assert.Equal("1", values["framerate"]);
    }

    /// <summary>
    ///     Verifies that RetroArch settings are correctly injected into a quoted key-value config file.
    /// </summary>
    [Fact]
    public void RetroArchInjectsQuotedSettingsCorrectly()
    {
        CopySampleToEmuDir("RetroArch", "Retroarch", "retroarch.cfg");

        var settings = CreateSettingsManager();
        settings.RetroArch.Fullscreen = true;
        settings.RetroArch.Vsync = false;
        settings.RetroArch.VideoDriver = "vulkan";
        settings.RetroArch.ThreadedVideo = true;
        settings.RetroArch.Bilinear = false;
        settings.RetroArch.AspectRatioIndex = "3";
        settings.RetroArch.ScaleInteger = true;
        settings.RetroArch.ShaderEnable = false;
        settings.RetroArch.HardSync = true;
        settings.RetroArch.AudioEnable = false;
        settings.RetroArch.AudioMute = true;
        settings.RetroArch.PauseNonActive = false;
        settings.RetroArch.SaveOnExit = false;
        settings.RetroArch.AutoSaveState = true;
        settings.RetroArch.AutoLoadState = true;
        settings.RetroArch.Rewind = true;
        settings.RetroArch.RunAhead = true;
        settings.RetroArch.DiscordAllow = false;
        settings.RetroArch.MenuDriver = "rgui";
        settings.RetroArch.ShowAdvancedSettings = false;
        settings.RetroArch.CheevosEnable = true;
        settings.RetroArch.CheevosHardcore = true;

        var emuDir = Path.Combine(_testDirectory, "RetroArch");
        RetroArchConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var lines = File.ReadAllLines(Path.Combine(emuDir, "retroarch.cfg"));
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;

            var parts = trimmed.Split('=', 2);
            if (parts.Length >= 1) values[parts[0].Trim()] = parts.Length > 1 ? parts[1].Trim() : "";
        }

        Assert.Equal("\"true\"", values["video_fullscreen"]);
        Assert.Equal("\"false\"", values["video_vsync"]);
        Assert.Equal("\"vulkan\"", values["video_driver"]);
        Assert.Equal("\"true\"", values["video_threaded"]);
        Assert.Equal("\"false\"", values["video_smooth"]);
        Assert.Equal("\"3\"", values["video_aspect_ratio_index"]);
        Assert.Equal("\"true\"", values["video_scale_integer"]);
        Assert.Equal("\"false\"", values["video_shader_enable"]);
        Assert.Equal("\"true\"", values["video_hard_sync"]);
        Assert.Equal("\"false\"", values["audio_enable"]);
        Assert.Equal("\"true\"", values["audio_mute_enable"]);
        Assert.Equal("\"false\"", values["pause_nonactive"]);
        Assert.Equal("\"false\"", values["config_save_on_exit"]);
        Assert.Equal("\"true\"", values["savestate_auto_save"]);
        Assert.Equal("\"true\"", values["savestate_auto_load"]);
        Assert.Equal("\"true\"", values["rewind_enable"]);
        Assert.Equal("\"true\"", values["run_ahead_enabled"]);
        Assert.Equal("\"false\"", values["discord_allow"]);
        Assert.Equal("\"rgui\"", values["menu_driver"]);
        Assert.Equal("\"false\"", values["menu_show_advanced_settings"]);
        Assert.Equal("\"true\"", values["cheevos_enable"]);
        Assert.Equal("\"true\"", values["cheevos_hardcore_mode_enable"]);
    }

    /// <summary>
    ///     Verifies that Blastem settings are correctly injected into a nested block config file.
    /// </summary>
    [Fact]
    public void BlastemInjectsNestedBlockSettingsCorrectly()
    {
        CopySampleToEmuDir("Blastem", "Blastem", "default.cfg");

        var settings = CreateSettingsManager();
        settings.Blastem.Fullscreen = true;
        settings.Blastem.Vsync = false;
        settings.Blastem.Aspect = "16:9";
        settings.Blastem.Scaling = "integer";
        settings.Blastem.Scanlines = true;
        settings.Blastem.AudioRate = 44100;
        settings.Blastem.SyncSource = "video";

        var emuDir = Path.Combine(_testDirectory, "Blastem");
        BlastemConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "default.cfg");
        var content = File.ReadAllText(configPath);

        Assert.Contains("fullscreen on", content, StringComparison.Ordinal);
        Assert.Contains("vsync off", content, StringComparison.Ordinal);
        Assert.Contains("aspect 16:9", content, StringComparison.Ordinal);
        Assert.Contains("scaling integer", content, StringComparison.Ordinal);
        Assert.Contains("scanlines on", content, StringComparison.Ordinal);
        Assert.Contains("rate 44100", content, StringComparison.Ordinal);
        Assert.Contains("sync_source video", content, StringComparison.Ordinal);
    }

    // --- Helpers ---

    /// <summary>
    ///     Parses an INI file with sections into a dictionary keyed by (section, key).
    /// </summary>
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