using Moq;
using SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Linux/macOS regression tests for LB-03: PowerShell Mount-DiskImage is Windows-only, so
///     launching an ISO DOS game must let DOSBox mount the image with 'imgmount' and list the
///     executables directly from the ISO9660 directory instead of failing with an error-mounting
///     dialog.
/// </summary>
public class DosBoxIsoLinuxTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sl_dosboxiso_" + Guid.NewGuid().ToString("N"));

    public DosBoxIsoLinuxTests()
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
    public async Task DosBoxIso_OnLinux_MountsTheIsoWithImgmountWithoutPowerShell()
    {
        if (OperatingSystem.IsWindows()) return;

        var isoPath = WriteIso(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["GAME.EXE"] = new byte[1234] });

        var (strategy, launcher, messageBox, mountIso) = CreateStrategy();
        string? confPath = null;
        string? confContent = null;
        launcher
            .Setup(l => l.LaunchRegularEmulatorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
                It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()))
            .Callback<string, string, ISystemManager, Emulator, string, IWindowContext, ILoadingState?, string?>((path, _, _, _, _, _, _, _) =>
                {
                    confPath = path;
                    confContent = File.Exists(path) ? File.ReadAllText(path) : null;
                })
            .Returns(Task.CompletedTask);

        await strategy.ExecuteAsync(CreateContext(isoPath), launcher.Object);

        Assert.NotNull(confPath);
        Assert.NotNull(confContent);
        Assert.Contains("imgmount d \"", confContent);
        Assert.Contains("\" -t iso", confContent);
        Assert.Contains("GAME.EXE", confContent);

        // The user's ISO is mounted directly (no conversion, no copy) and is never deleted.
        Assert.Equal(isoPath, ExtractImgmountPath(confContent));
        Assert.True(File.Exists(isoPath));
        Assert.False(File.Exists(confPath));

        // The PowerShell mount/dismount path is Windows-only and must not be touched.
        mountIso.Verify(m => m.ExecutePowerShellMountCommandAsync(
            It.IsAny<string>(), It.IsAny<ILogger>(), It.IsAny<IMessageBoxLibraryService>()), Times.Never);
        mountIso.Verify(m => m.ExecutePowerShellDismountCommandAsync(
            It.IsAny<string>(), It.IsAny<ILogger>(), It.IsAny<IMessageBoxLibraryService>()), Times.Never);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        messageBox.Verify(m => m.CouldNotFindAFileMessageBoxAsync(), Times.Never);
        messageBox.Verify(m => m.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
        messageBox.Verify(m => m.CouldNotLaunchThisGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DosBoxIso_OnLinux_NestedExecutable_ChangesDirectoryInConf()
    {
        if (OperatingSystem.IsWindows()) return;

        var isoPath = WriteIso(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["SUB/PLAY.BAT"] = new byte[99] });

        var (strategy, launcher, _, _) = CreateStrategy();
        string? confContent = null;
        CaptureConf(launcher, content => confContent = content);

        await strategy.ExecuteAsync(CreateContext(isoPath), launcher.Object);

        Assert.NotNull(confContent);
        Assert.Contains("imgmount d \"", confContent);
        Assert.Contains("\" -t iso", confContent);
        Assert.Contains("cd SUB", confContent);
        Assert.Contains("PLAY.BAT", confContent);
    }

    [Fact]
    public async Task DosBoxIso_OnLinux_NoExecutable_ReportsMissingFile()
    {
        if (OperatingSystem.IsWindows()) return;

        var isoPath = WriteIso(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["README.TXT"] = new byte[42] });

        var (strategy, launcher, messageBox, mountIso) = CreateStrategy();

        await strategy.ExecuteAsync(CreateContext(isoPath), launcher.Object);

        messageBox.Verify(m => m.CouldNotFindAFileMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        mountIso.Verify(m => m.ExecutePowerShellMountCommandAsync(
            It.IsAny<string>(), It.IsAny<ILogger>(), It.IsAny<IMessageBoxLibraryService>()), Times.Never);
        launcher.Verify(l => l.LaunchRegularEmulatorAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
            It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()), Times.Never);
    }

    private static (DosBoxLaunchStrategy Strategy, Mock<ILauncherService> Launcher,
        Mock<IMessageBoxLibraryService> MessageBox, Mock<IMountIsoFiles> MountIso) CreateStrategy()
    {
        var messageBox = TestDependencies.MessageBox();
        var mountIso = new Mock<IMountIsoFiles>();
        var launcher = new Mock<ILauncherService>();

        var strategy = new DosBoxLaunchStrategy(
            new Mock<IExtractionService>().Object,
            TestEnvironment.ConfigurationFromJson("{}"),
            messageBox.Object,
            new Mock<IMountChdFiles>().Object,
            mountIso.Object,
            new Mock<IDiscConverter>().Object,
            TestDependencies.Logger().Object);

        return (strategy, launcher, messageBox, mountIso);
    }

    private static void CaptureConf(Mock<ILauncherService> launcher, Action<string?> onConf)
    {
        launcher
            .Setup(l => l.LaunchRegularEmulatorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
                It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()))
            .Callback<string, string, ISystemManager, Emulator, string, IWindowContext, ILoadingState?, string?>(
                (path, _, _, _, _, _, _, _) => onConf(File.Exists(path) ? File.ReadAllText(path) : null))
            .Returns(Task.CompletedTask);
    }

    private static string ExtractImgmountPath(string confContent)
    {
        var line = confContent.Split('\n').First(l => l.StartsWith("imgmount", StringComparison.Ordinal)).Trim();
        var start = line.IndexOf('"');
        var end = line.LastIndexOf('"');
        return line[(start + 1)..end];
    }

    private static LaunchContext CreateContext(string isoPath)
    {
        return new LaunchContext
        {
            FilePath = isoPath,
            ResolvedFilePath = isoPath,
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

    private string WriteIso(IReadOnlyDictionary<string, byte[]> files)
    {
        var isoPath = Path.Combine(_root, "disc.iso");
        File.WriteAllBytes(isoPath, TestIso9660ImageBuilder.Build(files));
        return isoPath;
    }
}
