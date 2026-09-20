using SimpleLauncher.Core.Services.RetroAchievements;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="RetroAchievementsEmulatorConfiguratorService" />: sample restore and
///     credential injection into the emulator INI files, including the UTF-8 BOM regression where
///     strict INI parsers discarded the first line of the written file.
/// </summary>
public class RetroAchievementsEmulatorConfiguratorServiceTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), $"SL_RaConfigurator_{Guid.NewGuid():N}");

    private readonly RetroAchievementsEmulatorConfiguratorService _service = new(new NoOpLogger(), new NoOpLogger());

    public RetroAchievementsEmulatorConfiguratorServiceTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     The bundled sample folder is "Retroarch" while the configurator asks for "retroarch";
    ///     the sample restore must resolve it case-insensitively on case-sensitive file systems.
    ///     The written INI must not start with a UTF-8 BOM (strict parsers drop the first line).
    /// </summary>
    [Fact]
    public void ConfigureRetroArch_WritesIniWithoutBom()
    {
        var emuDir = Path.Combine(_testDirectory, "retroarch");
        Directory.CreateDirectory(emuDir);

        var result = _service.ConfigureRetroArch(Path.Combine(emuDir, "retroarch.exe"), "user", "pass");

        Assert.True(result);
        var configPath = Path.Combine(emuDir, "retroarch.cfg");
        AssertNoUtf8Bom(configPath);

        var content = File.ReadAllText(configPath);
        Assert.Contains("cheevos_enable = \"true\"", content, StringComparison.Ordinal);
        Assert.Contains("cheevos_username = \"user\"", content, StringComparison.Ordinal);
        Assert.Contains("cheevos_password = \"pass\"", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A portable PCSX2 install (portable.ini next to the executable) must be configured in
    ///     its local PCSX2.ini and the sectioned INI writer must not emit a UTF-8 BOM either.
    /// </summary>
    [Fact]
    public void ConfigurePcsx2_PortableInstall_WritesIniWithoutBom()
    {
        var emuDir = Path.Combine(_testDirectory, "pcsx2");
        Directory.CreateDirectory(emuDir);
        File.WriteAllText(Path.Combine(emuDir, "portable.ini"), "");

        var result = _service.ConfigurePcsx2(Path.Combine(emuDir, "pcsx2-qt.exe"), "user", "token");

        Assert.True(result);
        // Portable PCSX2 keeps its config in the local inis\ folder.
        var configPath = Path.Combine(emuDir, "inis", "PCSX2.ini");
        AssertNoUtf8Bom(configPath);

        var content = File.ReadAllText(configPath);
        Assert.Contains("Username = user", content, StringComparison.Ordinal);
        Assert.Contains("Token = token", content, StringComparison.Ordinal);
    }

    private static void AssertNoUtf8Bom(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        Assert.False(hasBom, $"'{path}' must not start with a UTF-8 BOM");
    }
}