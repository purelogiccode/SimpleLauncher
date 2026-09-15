using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Services.GameLauncher;
using SimpleLauncher.Avalonia.Services.PlayHistory;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UsageStats;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests the launch-gating contract: a config handler returning false aborts the
///     launch and clears the loading state (WPF parity), while a returning-true handler
///     lets the flow continue to the pre-flight checks. The database is redirected to
///     an isolated temp file (exclusive collection) so system lookups never depend
///     on real user data.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class MinimalLauncherServiceGatingTests : IDisposable
{
    private readonly Mock<IEmulatorConfigHandler> _handler = new();
    private readonly string _isoPath;
    private readonly FakeLoadingState _loading = new();
    private readonly Mock<IMessageBoxLibraryService> _messageBox = new();
    private readonly string _tempDir;

    public MinimalLauncherServiceGatingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SLTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        UnifiedTestDatabase.RedirectToTempDb(_tempDir);
        _isoPath = Path.Combine(_tempDir, "game.iso");
        File.WriteAllText(_isoPath, "fake iso");
    }

    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
            // best effort cleanup
        }
    }

    private LauncherService CreateLauncher(IEnumerable<IEmulatorConfigHandler> handlers)
    {
        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger>().Object;
        var systemManager = new SystemManagerService(config);

        var askAi = new AskAiToFixParameters(
            _messageBox.Object,
            new Mock<IParameterResolverService>().Object,
            new Mock<ISystemConfigurationWriterService>().Object,
            systemManager,
            logger,
            new Services.LocalizationService());

        var settings = new SettingsManagerService(
            config, logger, new Mock<ICredentialProtector>().Object, _messageBox.Object);

        return new LauncherService(
            _messageBox.Object,
            handlers,
            config,
            new Mock<IExtractionService>().Object,
            new Mock<IMountXisoFiles>().Object,
            new Mock<IMountChdFiles>().Object,
            new Mock<IMountZipFiles>().Object,
            askAi,
            settings,
            [new DefaultLaunchStrategy()],
            new PlayHistoryManager(),
            new Mock<Stats>(new Mock<IHttpClientFactory>().Object, config, logger).Object,
            new Mock<GamePadController>(_messageBox.Object, config, logger).Object,
            new Services.LocalizationService());
    }

    private static Emulator CreateEmulator(string name)
    {
        return new Emulator
        {
            EmulatorName = name,
            EmulatorLocation = "/usr/bin/fake-emulator",
            EmulatorParameters = "",
            ReceiveANotificationOnEmulatorError = false,
            ImagePackDownloadLink = "",
            ImagePackDownloadLink2 = "",
            ImagePackDownloadLink3 = "",
            ImagePackDownloadLink4 = "",
            ImagePackDownloadLink5 = "",
            ImagePackDownloadExtractPath = ""
        };
    }

    private static ISystemManager CreateSystem(string emulatorName, string folderPath)
    {
        return Mock.Of<ISystemManager>(s =>
            s.SystemName == "Test System" &&
            s.SystemFolders == new List<string> { folderPath } &&
            s.FileFormatsToLaunch == new List<string> { ".iso" } &&
            s.Emulators == new List<Emulator> { CreateEmulator(emulatorName) });
    }

    [Fact]
    public async Task HandlerReturningFalse_AbortsLaunchAndClearsLoadingState()
    {
        _handler.Setup(h => h.IsMatch(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _handler.Setup(h => h.HandleConfigurationAsync(It.IsAny<LaunchContext>())).ReturnsAsync(false);

        var launcher = CreateLauncher([_handler.Object]);

        await launcher.HandleButtonClickAsync(
            _isoPath,
            "TestEmulator",
            "Test System",
            CreateSystem("TestEmulator", _tempDir),
            CreateEmulator("TestEmulator"),
            "game.iso",
            new Mock<IWindowContext>().Object,
            _loading);

        // Handler ran with a populated context…
        _handler.Verify(h => h.HandleConfigurationAsync(It.Is<LaunchContext>(c =>
            c.EmulatorName == "TestEmulator" &&
            c.ResolvedFilePath == _isoPath)), Times.Once);

        // …the "Configuring emulator..." state was shown, then cleared, and nothing else ran.
        Assert.Contains(_loading.Calls, c => c is (true, "Configuring emulator..."));
        Assert.False(_loading.Calls[^1].IsLoading);
        _messageBox.Verify(m => m.CustomErrorMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandlerReturningTrue_ProceedsPastHandlersToPreflight()
    {
        _handler.Setup(h => h.IsMatch(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _handler.Setup(h => h.HandleConfigurationAsync(It.IsAny<LaunchContext>())).ReturnsAsync(true);

        var launcher = CreateLauncher([_handler.Object]);

        // "RetroArch Test" without a -L parameter trips the pre-flight check AFTER handlers,
        // proving the launch continued past the handler block (and we never Process.Start).
        await launcher.HandleButtonClickAsync(
            _isoPath,
            "RetroArch Test",
            "Test System",
            CreateSystem("RetroArch Test", _tempDir),
            CreateEmulator("RetroArch Test"),
            "game.iso",
            new Mock<IWindowContext>().Object,
            _loading);

        _messageBox.Verify(m => m.CustomErrorMessageBoxAsync(
            It.Is<string>(t => t.Contains("-L", StringComparison.Ordinal)), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task NoMatchingHandler_SkipsHandlerBlock()
    {
        _handler.Setup(h => h.IsMatch(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var launcher = CreateLauncher([_handler.Object]);

        await launcher.HandleButtonClickAsync(
            _isoPath,
            "RetroArch Test",
            "Test System",
            CreateSystem("RetroArch Test", _tempDir),
            CreateEmulator("RetroArch Test"),
            "game.iso",
            new Mock<IWindowContext>().Object,
            _loading);

        _handler.Verify(h => h.HandleConfigurationAsync(It.IsAny<LaunchContext>()), Times.Never);
        // Flow reached pre-flight directly (no handler matched).
        _messageBox.Verify(m => m.CustomErrorMessageBoxAsync(
            It.Is<string>(t => t.Contains("-L", StringComparison.Ordinal)), It.IsAny<string>()), Times.Once);
    }

    private sealed class FakeLoadingState : ILoadingState
    {
        public List<(bool IsLoading, string? Message)> Calls { get; } = [];

        public void SetLoadingState(bool isLoading, string? message = null)
        {
            Calls.Add((isLoading, message));
        }
    }
}