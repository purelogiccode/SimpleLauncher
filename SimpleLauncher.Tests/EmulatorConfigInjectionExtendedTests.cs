using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Services.InjectEmulatorConfig;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Tests.TestHelpers;
using Tomlyn;
using Tomlyn.Model;
using Xunit;
using BlastemConfigurationService = SimpleLauncher.Core.Services.InjectEmulatorConfig.BlastemConfigurationService;

namespace SimpleLauncher.Tests;

/// <summary>
///     Extended tests for emulator configuration injection covering disabled/false-value edge cases for all emulators.
/// </summary>
[SuppressMessage("ReSharper", "NullableWarningSuppressionIsUsed")]
public class EmulatorConfigInjectionExtendedTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly NoOpCredentialProtector _credentialProtector = new();
    private readonly ILogger _logErrors = new NoOpLogger();
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of <see cref="EmulatorConfigInjectionExtendedTests" /> with in-memory configuration and
    ///     a temporary test directory.
    /// </summary>
    public EmulatorConfigInjectionExtendedTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Urls:YouTubeSearch"] = "https://www.youtube.com/results?search_query=",
                ["Urls:IgdbSearch"] = "https://www.igdb.com/search?q="
            })
            .Build();

        ServiceProviderMock.Install();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_EmuInjectionExt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the test directory and restores the service provider mock.
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

    // DuckStation edge cases

    /// <summary>
    ///     Verifies that DuckStation writes false values for all boolean options when disabled.
    /// </summary>
    [Fact]
    public void DuckStationDisabledOptionsUsesFalseValues()
    {
        var emuDir = Path.Combine(_testDirectory, "DuckStation");
        Directory.CreateDirectory(emuDir);
        File.WriteAllText(Path.Combine(emuDir, "portable.txt"), string.Empty);
        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "DuckStation", "settings.ini");
        File.Copy(samplePath, Path.Combine(emuDir, "settings.ini"));

        var settings = CreateSettingsManager();
        settings.DuckStation.StartFullscreen = false;
        settings.DuckStation.PauseOnFocusLoss = false;
        settings.DuckStation.SaveStateOnExit = false;
        settings.DuckStation.WidescreenHack = false;
        settings.DuckStation.PgxpEnable = false;
        settings.DuckStation.Vsync = false;
        settings.DuckStation.OutputMuted = false;

        DuckStationConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var lines = File.ReadAllLines(Path.Combine(emuDir, "settings.ini")).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("false", sectionValues[("Main", "StartFullscreen")]);
        Assert.Equal("false", sectionValues[("Main", "PauseOnFocusLoss")]);
        Assert.Equal("false", sectionValues[("Main", "SaveStateOnExit")]);
        Assert.Equal("false", sectionValues[("GPU", "WidescreenHack")]);
        Assert.Equal("false", sectionValues[("GPU", "PGXPEnable")]);
        Assert.Equal("false", sectionValues[("Display", "VSync")]);
        Assert.Equal("false", sectionValues[("Audio", "OutputMuted")]);
    }

    // PCSX2 edge cases

    /// <summary>
    ///     Verifies that PCSX2 writes false values for all boolean options when disabled.
    /// </summary>
    [Fact]
    public void Pcsx2DisabledOptionsUsesFalseValues()
    {
        var emuDir = Path.Combine(_testDirectory, "PCSX2");
        Directory.CreateDirectory(emuDir);
        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "PCSX2", "PCSX2.ini");
        File.Copy(samplePath, Path.Combine(emuDir, "PCSX2.ini"));

        var settings = CreateSettingsManager();
        settings.Pcsx2.StartFullscreen = false;
        settings.Pcsx2.EnableCheats = false;
        settings.Pcsx2.EnableWidescreenPatches = false;
        settings.Pcsx2.Vsync = false;
        settings.Pcsx2.AchievementsEnabled = false;
        settings.Pcsx2.AchievementsHardcore = false;

        Pcsx2ConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var lines = File.ReadAllLines(Path.Combine(emuDir, "PCSX2.ini")).ToList();
        var sectionValues = ParseIniSections(lines);

        Assert.Equal("false", sectionValues[("UI", "StartFullscreen")]);
        Assert.Equal("false", sectionValues[("EmuCore", "EnableCheats")]);
        Assert.Equal("false", sectionValues[("EmuCore", "EnableWideScreenPatches")]);
        Assert.Equal("false", sectionValues[("EmuCore/GS", "VsyncEnable")]);
        Assert.Equal("false", sectionValues[("Achievements", "Enabled")]);
        Assert.Equal("false", sectionValues[("Achievements", "Hardcore")]);
    }

    // Mesen edge cases

    /// <summary>
    ///     Verifies that Mesen writes false values for all boolean options when disabled.
    /// </summary>
    [Fact]
    public void MesenDisabledOptionsUsesFalseValues()
    {
        CopySampleToEmuDir("Mesen", "Mesen", "settings.json");

        var settings = CreateSettingsManager();
        settings.Mesen.Fullscreen = false;
        settings.Mesen.Vsync = true;
        settings.Mesen.Bilinear = true;
        settings.Mesen.EnableAudio = true;
        settings.Mesen.Rewind = false;
        settings.Mesen.PauseInBackground = false;

        var emuDir = Path.Combine(_testDirectory, "Mesen");
        MesenConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "settings.json");
        var root = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();

        var video = root["Video"]!.AsObject();
        Assert.False((bool)video["UseExclusiveFullscreen"]!);
        Assert.True((bool)video["VerticalSync"]!);
        Assert.True((bool)video["UseBilinearInterpolation"]!);

        var audio = root["Audio"]!.AsObject();
        Assert.True((bool)audio["EnableAudio"]!);

        var preferences = root["Preferences"]!.AsObject();
        Assert.False((bool)preferences["EnableRewind"]!);
        Assert.False((bool)preferences["PauseWhenInBackground"]!);
    }

    // Xenia edge cases

    /// <summary>
    ///     Verifies that Xenia writes false values for all boolean options when disabled.
    /// </summary>
    [Fact]
    public void XeniaDisabledOptionsUsesFalseValues()
    {
        CopySampleToEmuDir("Xenia", "Xenia", "xenia.config.toml");

        var settings = CreateSettingsManager();
        settings.Xenia.Mute = false;
        settings.Xenia.Vsync = true;
        settings.Xenia.Fullscreen = false;
        settings.Xenia.Vibration = true;
        settings.Xenia.DiscordPresence = true;
        settings.Xenia.ApplyPatches = false;
        settings.Xenia.GammaSrgb = false;
        settings.Xenia.MountCache = true;

        var emuDir = Path.Combine(_testDirectory, "Xenia");
        XeniaConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "xenia.config.toml");
        var tomlContent = File.ReadAllText(configPath);
        var model = TomlSerializer.Deserialize<TomlTable>(tomlContent) ?? new TomlTable();

        var apu = (TomlTable)model["APU"];
        Assert.False((bool)apu["mute"]);

        var gpu = (TomlTable)model["GPU"];
        Assert.True((bool)gpu["vsync"]);

        var display = (TomlTable)model["Display"];
        Assert.False((bool)display["fullscreen"]);

        var hid = (TomlTable)model["HID"];
        Assert.True((bool)hid["vibration"]);

        var general = (TomlTable)model["General"];
        Assert.True((bool)general["discord"]);
        Assert.False((bool)general["apply_patches"]);

        var storage = (TomlTable)model["Storage"];
        Assert.True((bool)storage["mount_cache"]);
    }

    // RPCS3 edge cases

    /// <summary>
    ///     Verifies that RPCS3 writes false values for all boolean options when disabled.
    /// </summary>
    [Fact]
    public void Rpcs3DisabledOptionsUsesFalseValues()
    {
        CopySampleToEmuDir("RPCS3", "RPCS3", "config.yml");

        var settings = CreateSettingsManager();
        settings.Rpcs3.Vsync = false;
        settings.Rpcs3.AudioBuffering = false;
        settings.Rpcs3.StartFullscreen = false;

        var emuDir = Path.Combine(_testDirectory, "RPCS3");
        Rpcs3ConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "config.yml");
        var content = File.ReadAllText(configPath);

        Assert.Contains("VSync: false", content, StringComparison.Ordinal);
        Assert.Contains("Enable Buffering: false", content, StringComparison.Ordinal);
        Assert.Contains("Start games in fullscreen mode: false", content, StringComparison.Ordinal);
    }

    // Redream edge cases

    /// <summary>
    ///     Verifies that Redream uses numeric zero/false values for disabled boolean options.
    /// </summary>
    [Fact]
    public void RedreamDisabledOptionsUsesZeroValues()
    {
        CopySampleToEmuDir("Redream", "Redream", "redream.cfg");

        var settings = CreateSettingsManager();
        settings.Redream.Vsync = true;
        settings.Redream.Frameskip = true;
        settings.Redream.Framerate = false;

        var emuDir = Path.Combine(_testDirectory, "Redream");
        RedreamConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var lines = File.ReadAllLines(Path.Combine(emuDir, "redream.cfg"));
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2) values[parts[0].Trim()] = parts[1].Trim();
        }

        Assert.Equal("1", values["vsync"]);
        Assert.Equal("1", values["frameskip"]);
        Assert.Equal("0", values["framerate"]);
    }

    // RetroArch edge cases

    /// <summary>
    ///     Verifies that RetroArch writes false values for all boolean options when disabled.
    /// </summary>
    [Fact]
    public void RetroArchDisabledOptionsUsesFalseValues()
    {
        CopySampleToEmuDir("RetroArch", "Retroarch", "retroarch.cfg");

        var settings = CreateSettingsManager();
        settings.RetroArch.Fullscreen = false;
        settings.RetroArch.Vsync = true;
        settings.RetroArch.ThreadedVideo = false;
        settings.RetroArch.Bilinear = true;
        settings.RetroArch.ScaleInteger = false;
        settings.RetroArch.ShaderEnable = true;
        settings.RetroArch.HardSync = false;
        settings.RetroArch.AudioEnable = true;
        settings.RetroArch.AudioMute = false;
        settings.RetroArch.PauseNonActive = true;
        settings.RetroArch.SaveOnExit = true;
        settings.RetroArch.AutoSaveState = false;
        settings.RetroArch.AutoLoadState = false;
        settings.RetroArch.Rewind = false;
        settings.RetroArch.RunAhead = false;
        settings.RetroArch.DiscordAllow = true;
        settings.RetroArch.ShowAdvancedSettings = true;
        settings.RetroArch.CheevosEnable = false;
        settings.RetroArch.CheevosHardcore = false;

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

        Assert.Equal("\"false\"", values["video_fullscreen"]);
        Assert.Equal("\"true\"", values["video_vsync"]);
        Assert.Equal("\"false\"", values["video_threaded"]);
        Assert.Equal("\"true\"", values["video_smooth"]);
        Assert.Equal("\"false\"", values["video_scale_integer"]);
        Assert.Equal("\"true\"", values["video_shader_enable"]);
        Assert.Equal("\"false\"", values["video_hard_sync"]);
        Assert.Equal("\"true\"", values["audio_enable"]);
        Assert.Equal("\"false\"", values["audio_mute_enable"]);
        Assert.Equal("\"true\"", values["pause_nonactive"]);
        Assert.Equal("\"true\"", values["config_save_on_exit"]);
        Assert.Equal("\"false\"", values["savestate_auto_save"]);
        Assert.Equal("\"false\"", values["savestate_auto_load"]);
        Assert.Equal("\"false\"", values["rewind_enable"]);
        Assert.Equal("\"false\"", values["run_ahead_enabled"]);
        Assert.Equal("\"true\"", values["discord_allow"]);
        Assert.Equal("\"true\"", values["menu_show_advanced_settings"]);
        Assert.Equal("\"false\"", values["cheevos_enable"]);
        Assert.Equal("\"false\"", values["cheevos_hardcore_mode_enable"]);
    }

    // Blastem edge cases

    /// <summary>
    ///     Verifies that Blastem uses "off" values for disabled boolean options.
    /// </summary>
    [Fact]
    public void BlastemDisabledOptionsUsesOffValues()
    {
        CopySampleToEmuDir("Blastem", "Blastem", "default.cfg");

        var settings = CreateSettingsManager();
        settings.Blastem.Fullscreen = false;
        settings.Blastem.Vsync = true;
        settings.Blastem.Scanlines = false;

        var emuDir = Path.Combine(_testDirectory, "Blastem");
        BlastemConfigurationService.InjectSettings(FakeEmulatorExePath(emuDir), settings, Log.Logger);

        var configPath = Path.Combine(emuDir, "default.cfg");
        var content = File.ReadAllText(configPath);

        Assert.Contains("fullscreen off", content, StringComparison.Ordinal);
        Assert.Contains("vsync on", content, StringComparison.Ordinal);
        Assert.Contains("scanlines off", content, StringComparison.Ordinal);
    }

    // Helper

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