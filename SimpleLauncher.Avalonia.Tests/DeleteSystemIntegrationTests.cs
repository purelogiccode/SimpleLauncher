using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Services.SystemConfiguration;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Integration tests for the "Delete System" context-menu path (WPF parity).
///     Verifies that SystemConfigurationWriterService.DeleteSystemAsync removes the
///     correct SystemConfig node and that the subsequent orchestrator reload reflects
///     the deletion (regression guard for the AreYouSure... stub that previously
///     returned Ok and prevented deletion).
///     The database is redirected to an isolated temp file (exclusive collection);
///     after every legacy XML write the test database is re-seeded from the XML so
///     manager reads stay in sync without touching real user data.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class DeleteSystemIntegrationTests : IDisposable
{
    private readonly IConfiguration _config;
    private readonly Mock<ILogger> _logger;
    private readonly string _systemXmlPath;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_Delete_{Guid.NewGuid():N}");

    public DeleteSystemIntegrationTests()
    {
        _systemXmlPath = Path.Combine(_tempRoot, "system.xml");
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(_systemXmlPath, """
                                          <SystemConfigs>
                                            <SystemConfig>
                                              <SystemName>Atari 2600</SystemName>
                                              <SystemFolders><SystemFolder>roms/Atari2600</SystemFolder></SystemFolders>
                                              <SystemImageFolder>images/Atari2600</SystemImageFolder>
                                              <FileFormatsToSearch><FormatToSearch>.zip</FormatToSearch></FileFormatsToSearch>
                                              <FileFormatsToLaunch><FormatToLaunch>.zip</FormatToLaunch></FileFormatsToLaunch>
                                              <Emulators><Emulator><EmulatorName>Stella</EmulatorName><EmulatorPath>stella.exe</EmulatorPath><EmulatorParameters></EmulatorParameters></Emulator></Emulators>
                                            </SystemConfig>
                                            <SystemConfig>
                                              <SystemName>NES</SystemName>
                                              <SystemFolders><SystemFolder>roms/NES</SystemFolder></SystemFolders>
                                              <SystemImageFolder>images/NES</SystemImageFolder>
                                              <FileFormatsToSearch><FormatToSearch>.nes</FormatToSearch></FileFormatsToSearch>
                                              <FileFormatsToLaunch><FormatToLaunch>.nes</FormatToLaunch></FileFormatsToLaunch>
                                              <Emulators><Emulator><EmulatorName>Mesen</EmulatorName><EmulatorPath>mesen.exe</EmulatorPath><EmulatorParameters></EmulatorParameters></Emulator></Emulators>
                                            </SystemConfig>
                                            <SystemConfig>
                                              <SystemName>SNES</SystemName>
                                              <SystemFolders><SystemFolder>roms/SNES</SystemFolder></SystemFolders>
                                              <SystemImageFolder>images/SNES</SystemImageFolder>
                                              <FileFormatsToSearch><FormatToSearch>.sfc</FormatToSearch></FileFormatsToSearch>
                                              <FileFormatsToLaunch><FormatToLaunch>.sfc</FormatToLaunch></FileFormatsToLaunch>
                                              <Emulators><Emulator><EmulatorName>Snes9x</EmulatorName><EmulatorPath>snes9x.exe</EmulatorPath><EmulatorParameters></EmulatorParameters></Emulator></Emulators>
                                            </SystemConfig>
                                          </SystemConfigs>
                                          """);
        _config = TestEnvironment.ConfigurationFromJson(
            $$"""{"SystemXmlPath": "{{_systemXmlPath.Replace("\\", @"\\")}}"}""");
        _logger = TestDependencies.Logger();

        UnifiedTestDatabase.RedirectToTempDb(_tempRoot);
        UnifiedTestDatabase.SeedSystemsFromXml(new SystemManagerService(_config), _systemXmlPath);
    }

    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // ignored
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DeleteSystemAsync_RemovesCorrectNodeAndPreservesOthers()
    {
        var writer = new SystemConfigurationWriterService(_config, _logger.Object);
        await writer.DeleteSystemAsync("NES");

        var xml = File.ReadAllText(_systemXmlPath);
        Assert.DoesNotContain("<SystemName>NES</SystemName>", xml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<SystemName>Atari 2600</SystemName>", xml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<SystemName>SNES</SystemName>", xml, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(_systemXmlPath + ".tmp"), "Temp file should not remain after atomic move");
    }

    [Fact]
    public async Task DeleteSystemAsync_NonExistentSystem_DoesNotThrowAndPreservesFile()
    {
        var writer = new SystemConfigurationWriterService(_config, _logger.Object);
        var before = File.ReadAllText(_systemXmlPath);
        await writer.DeleteSystemAsync("NonExistent");
        var after = File.ReadAllText(_systemXmlPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task DeleteSystemAsync_ThenReload_ReflectsInSystemManager()
    {
        var writer = new SystemConfigurationWriterService(_config, _logger.Object);
        var manager = new SystemManagerService(_config);

        var before = manager.LoadSystems();
        Assert.Equal(3, before.Count);

        await writer.DeleteSystemAsync("Atari 2600");
        manager.InvalidateCache();
        UnifiedTestDatabase.SeedSystemsFromXml(manager, _systemXmlPath);
        var after = manager.LoadSystems();
        Assert.Equal(2, after.Count);
        Assert.DoesNotContain(after,
            m => string.Equals(m.SystemName, "Atari 2600", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(after, m => string.Equals(m.SystemName, "NES", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteSystemAsync_MultipleDeletes_SequentiallySucceed()
    {
        var writer = new SystemConfigurationWriterService(_config, _logger.Object);
        var manager = new SystemManagerService(_config);

        await writer.DeleteSystemAsync("NES");
        await writer.DeleteSystemAsync("SNES");
        manager.InvalidateCache();
        UnifiedTestDatabase.SeedSystemsFromXml(manager, _systemXmlPath);
        var remaining = manager.LoadSystems();
        Assert.Single(remaining);
        Assert.Equal("Atari 2600", remaining[0].SystemName);
    }
}