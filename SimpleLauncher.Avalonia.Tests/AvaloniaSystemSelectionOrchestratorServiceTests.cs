using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.DisplaySystemInfo;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Avalonia.Services.SystemSelectionOrchestrator;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the Avalonia SystemSelectionOrchestratorService: verifies system.xml
///     loading into the combo box, system selection coordination, and the full
///     ReloadAfterConfigurationChangeAsync flow (WPF SystemSelectionOrchestratorService parity).
///     The database is redirected to an isolated temp file (exclusive collection) and
///     seeded from the fixture XML so results never depend on real user data.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class AvaloniaSystemSelectionOrchestratorServiceTests : IDisposable
{
    private readonly IConfiguration _config;
    private readonly Mock<ISystemSelectionHost> _host;
    private readonly AvaloniaGameFileLoadingOrchestrator _loadingOrchestrator;
    private readonly Mock<ILogger> _logger;
    private readonly AvaloniaSystemSelectionOrchestratorService _service;
    private readonly SystemManagerService _systemManager;
    private readonly string _systemXmlPath;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_SysOrch_{Guid.NewGuid():N}");

    public AvaloniaSystemSelectionOrchestratorServiceTests()
    {
        _systemXmlPath = Path.Combine(_tempRoot, "system.xml");
        var romsFolder = Path.Combine(_tempRoot, "roms");
        Directory.CreateDirectory(romsFolder);

        File.WriteAllText(_systemXmlPath, """
                                          <SystemConfigs>
                                            <SystemConfig>
                                              <SystemName>Atari 2600</SystemName>
                                              <SystemFolders>
                                                <SystemFolder>roms/Atari2600</SystemFolder>
                                              </SystemFolders>
                                              <SystemImageFolder>images/Atari2600</SystemImageFolder>
                                              <FileFormatsToSearch>
                                                <FormatToSearch>.zip</FormatToSearch>
                                              </FileFormatsToSearch>
                                              <FileFormatsToLaunch>
                                                <FormatToLaunch>.zip</FormatToLaunch>
                                              </FileFormatsToLaunch>
                                              <Emulators>
                                                <Emulator>
                                                  <EmulatorName>Stella</EmulatorName>
                                                  <EmulatorPath>stella.exe</EmulatorPath>
                                                  <EmulatorParameters></EmulatorParameters>
                                                </Emulator>
                                              </Emulators>
                                            </SystemConfig>
                                            <SystemConfig>
                                              <SystemName>NES</SystemName>
                                              <SystemFolders>
                                                <SystemFolder>roms/NES</SystemFolder>
                                              </SystemFolders>
                                              <SystemImageFolder>images/NES</SystemImageFolder>
                                              <FileFormatsToSearch>
                                                <FormatToSearch>.nes</FormatToSearch>
                                              </FileFormatsToSearch>
                                              <FileFormatsToLaunch>
                                                <FormatToLaunch>.nes</FormatToLaunch>
                                              </FileFormatsToLaunch>
                                              <Emulators>
                                                <Emulator>
                                                  <EmulatorName>Mesen</EmulatorName>
                                                  <EmulatorPath>mesen.exe</EmulatorPath>
                                                  <EmulatorParameters></EmulatorParameters>
                                                </Emulator>
                                                <Emulator>
                                                  <EmulatorName>FCEUX</EmulatorName>
                                                  <EmulatorPath>fceux.exe</EmulatorPath>
                                                  <EmulatorParameters></EmulatorParameters>
                                                </Emulator>
                                              </Emulators>
                                             </SystemConfig>
                                          </SystemConfigs>
                                          """);

        _config = TestEnvironment.ConfigurationFromJson(
            $$"""{"SystemXmlPath": "{{_systemXmlPath.Replace("\\", @"\\")}}"}""");
        _logger = TestDependencies.Logger();

        _systemManager = new SystemManagerService(_config);
        _loadingOrchestrator = new AvaloniaGameFileLoadingOrchestrator(
            new AvaloniaGameCacheService(), _logger.Object);
        _host = new Mock<ISystemSelectionHost>();
        _host.Setup(h => h.GetSelectedSystem()).Returns((string?)null);

        _service = new AvaloniaSystemSelectionOrchestratorService(
            _systemManager, _loadingOrchestrator, _logger.Object);
        _service.Initialize(_host.Object);

        UnifiedTestDatabase.RedirectToTempDb(_tempRoot);
        UnifiedTestDatabase.SeedSystemsFromXml(_systemManager, _systemXmlPath);
    }

    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // best effort
        }

        GC.SuppressFinalize(this);
    }

    // ── LoadOrReloadSystemManager ─────────────────────────────────────────

    [Fact]
    public void LoadOrReloadSystemManager_PopulatesSystemComboBox()
    {
        _service.LoadOrReloadSystemManager();

        _host.Verify(h => h.SetSystemComboBoxItems(It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }

    // ── HandleSystemSelectionChanged ──────────────────────────────────────

    [Fact]
    public void HandleSystemSelectionChanged_NavigatesAndPopulatesEmulators()
    {
        _host.Setup(h => h.GetSelectedSystem()).Returns("NES");

        _service.HandleSystemSelectionChanged();

        _host.Verify(h => h.NavigateToSystemAsync("NES"), Times.Once);
        _host.Verify(h => h.SetEmulatorComboBoxItems(It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }

    [Fact]
    public void HandleSystemSelectionChanged_NullSelection_DoesNothing()
    {
        _host.Setup(h => h.GetSelectedSystem()).Returns((string?)null);

        _service.HandleSystemSelectionChanged();

        _host.Verify(h => h.NavigateToSystemAsync(It.IsAny<string>()), Times.Never);
        _host.Verify(h => h.SetEmulatorComboBoxItems(It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public void HandleSystemSelectionChanged_SingleEmulator_SelectsCorrectly()
    {
        _host.Setup(h => h.GetSelectedSystem()).Returns("Atari 2600");

        _service.HandleSystemSelectionChanged();

        _host.Verify(h => h.NavigateToSystemAsync("Atari 2600"), Times.Once);
        _host.Verify(h => h.SetEmulatorComboBoxItems(It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }

    [Fact]
    public void HandleSystemSelectionChanged_ShowsSystemInformation()
    {
        // WPF DisplaySystemInformation parity: the selected system's configuration
        // summary is shown until the user loads games with the letter buttons.
        var service = new AvaloniaSystemSelectionOrchestratorService(
            _systemManager, _loadingOrchestrator, _logger.Object, new AvaloniaDisplaySystemInformation());
        service.Initialize(_host.Object);
        _host.Setup(h => h.GetSelectedSystem()).Returns("NES");

        service.HandleSystemSelectionChanged();

        _host.Verify(h => h.ShowSystemInformation(
            It.Is<IReadOnlyList<SystemInfoLine>>(lines =>
                lines.Any(l => l.Text.Contains("System Folder", StringComparison.Ordinal)))), Times.Once);
    }

    // ── ReloadAfterConfigurationChangeAsync ───────────────────────────────

    [Fact]
    public async Task ReloadAfterConfigurationChange_RefreshesSidebarAndWatcher()
    {
        await _service.ReloadAfterConfigurationChangeAsync();

        _host.Verify(h => h.RefreshSidebar(), Times.Once);
        _host.Verify(h => h.RestartFileWatcher(), Times.Once);
    }

    [Fact]
    public async Task ReloadAfterConfigurationChange_ReloadsSystemComboBox()
    {
        await _service.ReloadAfterConfigurationChangeAsync();

        // After reload, the SystemComboBox should be repopulated
        _host.Verify(h => h.SetSystemComboBoxItems(It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }

    [Fact]
    public async Task ReloadAfterConfigurationChange_WithSelectedSystem_SyncsEmulators()
    {
        _host.Setup(h => h.GetSelectedSystem()).Returns("NES");

        await _service.ReloadAfterConfigurationChangeAsync();

        // The orchestrator should navigate, refresh sidebar, restart watcher, and
        // set emulator items for the selected system.
        _host.Verify(h => h.RefreshSidebar(), Times.Once);
        _host.Verify(h => h.RestartFileWatcher(), Times.Once);
        _host.Verify(h => h.SetEmulatorComboBoxItems(It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }

    [Fact]
    public async Task ReloadAfterConfigurationChange_NoSelection_ClearsEmulators()
    {
        _host.Setup(h => h.GetSelectedSystem()).Returns((string?)null);

        await _service.ReloadAfterConfigurationChangeAsync();

        _host.Verify(h => h.SetEmulatorComboBoxItems(
                It.Is<IReadOnlyList<string>>(names => names.Count == 0)),
            Times.Once);
    }

    // ── System.xml update reflects in subsequent Load ─────────────────────

    [Fact]
    public void LoadOrReloadSystemManager_AfterNewSystemAdded_IncludesNewSystem()
    {
        // Simulate adding a system: append to system.xml and invalidate cache
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
                                              <SystemName>Sega Genesis</SystemName>
                                              <SystemFolders><SystemFolder>roms/Genesis</SystemFolder></SystemFolders>
                                              <SystemImageFolder>images/Genesis</SystemImageFolder>
                                              <FileFormatsToSearch><FormatToSearch>.gen</FormatToSearch></FileFormatsToSearch>
                                              <FileFormatsToLaunch><FormatToLaunch>.gen</FormatToLaunch></FileFormatsToLaunch>
                                              <Emulators><Emulator><EmulatorName>BlastEm</EmulatorName><EmulatorPath>blastem.exe</EmulatorPath><EmulatorParameters></EmulatorParameters></Emulator></Emulators>
                                             </SystemConfig>
                                          </SystemConfigs>
                                          """);

        UnifiedTestDatabase.SeedSystemsFromXml(_systemManager, _systemXmlPath);
        _service.LoadOrReloadSystemManager();

        _host.Verify(h => h.SetSystemComboBoxItems(
                It.Is<IReadOnlyList<string>>(names => names.Count == 3)),
            Times.Once);
    }
}