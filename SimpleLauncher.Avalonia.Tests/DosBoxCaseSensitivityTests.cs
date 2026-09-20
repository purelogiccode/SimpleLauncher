using Moq;
using SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     LB-07 regression tests: DOSBox game-file discovery must be case-insensitive so that
///     scene archives written on Windows (GAME.EXE, AUTOEXEC.BAT) still launch on Linux, where
///     a case-sensitive "*.exe" glob would miss them and report "no executable found".
/// </summary>
public class DosBoxCaseSensitivityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sl_dosboxcase_" + Guid.NewGuid().ToString("N"));

    public DosBoxCaseSensitivityTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task DosBoxDirectory_UppercaseExecutable_IsDiscovered()
    {
        File.WriteAllText(Path.Combine(_root, "GAME.EXE"), "MZ");
        File.WriteAllText(Path.Combine(_root, "README.TXT"), "notes");

        var (strategy, launcher, messageBox) = CreateStrategy();
        string? confContent = null;
        launcher
            .Setup(l => l.LaunchRegularEmulatorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
                It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()))
            .Callback<string, string, ISystemManager, Emulator, string, IWindowContext, ILoadingState?, string?>(
                (path, _, _, _, _, _, _, _) => confContent = File.Exists(path) ? File.ReadAllText(path) : null)
            .Returns(Task.CompletedTask);

        await strategy.ExecuteAsync(CreateContext(_root), launcher.Object);

        Assert.NotNull(confContent);
        Assert.Contains("GAME.EXE", confContent);
        messageBox.Verify(m => m.CouldNotFindAFileMessageBoxAsync(), Times.Never);
        messageBox.Verify(m => m.CouldNotLaunchThisGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
    }

    private static (DosBoxLaunchStrategy Strategy, Mock<ILauncherService> Launcher,
        Mock<IMessageBoxLibraryService> MessageBox) CreateStrategy()
    {
        var messageBox = TestDependencies.MessageBox();
        var launcher = new Mock<ILauncherService>();

        var strategy = new DosBoxLaunchStrategy(
            new Mock<IExtractionService>().Object,
            TestEnvironment.ConfigurationFromJson("{}"),
            messageBox.Object,
            new Mock<IMountChdFiles>().Object,
            new Mock<IMountIsoFiles>().Object,
            new Mock<IDiscConverter>().Object,
            TestDependencies.Logger().Object);

        return (strategy, launcher, messageBox);
    }

    private static LaunchContext CreateContext(string directory)
    {
        return new LaunchContext
        {
            FilePath = directory,
            ResolvedFilePath = directory,
            EmulatorName = "DOSBox-X",
            SystemName = "DOS",
            SystemManagerService = Mock.Of<ISystemManager>(),
            EmulatorManager = new Emulator
            {
                EmulatorName = "DOSBox-X",
                EmulatorLocation = "/usr/bin/dosbox-x",
                EmulatorParameters = "",
                ImagePackDownloadLink = "",
                ImagePackDownloadLink2 = "",
                ImagePackDownloadLink3 = "",
                ImagePackDownloadLink4 = "",
                ImagePackDownloadLink5 = "",
                ImagePackDownloadExtractPath = ""
            },
            WindowContext = new Mock<IWindowContext>().Object
        };
    }
}
