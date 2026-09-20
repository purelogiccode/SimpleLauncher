using CHDSharp.Encoder;
using Moq;
using SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.Converters;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Linux/macOS regression tests for LB-02: CHDMounter/Dokan are Windows-only, so launching a
///     CHD DOS game must convert the image with the managed CHDSharp decoder, list the executables
///     of the cooked ISO9660 data track, and let DOSBox mount the converted image with 'imgmount'
///     instead of failing with an error-mounting dialog.
/// </summary>
public class DosBoxChdLinuxTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sl_dosboxchd_" + Guid.NewGuid().ToString("N"));

    public DosBoxChdLinuxTests()
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
    public async Task DosBoxChd_OnLinux_ConvertsListsAndLaunchesWithImgmount()
    {
        if (OperatingSystem.IsWindows()) return;

        var chdPath = CreateChd(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["GAME.EXE"] = new byte[1234] });

        var (strategy, launcher, messageBox, mountChd) = CreateStrategy();
        string? confPath = null;
        string? confContent = null;
        var imageExistedDuringLaunch = false;
        launcher
            .Setup(l => l.LaunchRegularEmulatorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
                It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()))
            .Callback<string, string, ISystemManager, Emulator, string, IWindowContext, ILoadingState?, string?>(
                (path, _, _, _, _, _, _, _) =>
                {
                    confPath = path;
                    confContent = File.Exists(path) ? File.ReadAllText(path) : null;
                    if (confContent != null) imageExistedDuringLaunch = File.Exists(ExtractImgmountPath(confContent));
                })
            .Returns(Task.CompletedTask);

        await strategy.ExecuteAsync(CreateContext(chdPath), launcher.Object);

        Assert.NotNull(confPath);
        Assert.NotNull(confContent);
        Assert.Contains("imgmount d \"", confContent);
        Assert.Contains("\" -t cdrom", confContent);
        Assert.Contains("GAME.EXE", confContent);

        // The single executable was auto-selected and DOSBox was pointed at the converted image.
        var imagePath = ExtractImgmountPath(confContent);
        Assert.EndsWith(".cue", imagePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(imageExistedDuringLaunch);

        // Converted image + conf are deleted once DOSBox has exited; the CHDMounter path was
        // never called.
        Assert.False(File.Exists(imagePath));
        Assert.False(File.Exists(Path.ChangeExtension(imagePath, ".bin")));
        Assert.False(File.Exists(confPath));
        mountChd.Verify(m => m.MountAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ILogger>(), It.IsAny<IMessageBoxLibraryService>()), Times.Never);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        messageBox.Verify(m => m.CouldNotFindAFileMessageBoxAsync(), Times.Never);
        messageBox.Verify(m => m.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
        messageBox.Verify(m => m.CouldNotLaunchThisGameMessageBoxAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DosBoxChd_OnLinux_NestedExecutable_ChangesDirectoryInConf()
    {
        if (OperatingSystem.IsWindows()) return;

        var chdPath = CreateChd(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["SUB/PLAY.BAT"] = new byte[99] });

        var (strategy, launcher, _, _) = CreateStrategy();
        string? confContent = null;
        CaptureConf(launcher, content => confContent = content);

        await strategy.ExecuteAsync(CreateContext(chdPath), launcher.Object);

        Assert.NotNull(confContent);
        Assert.Contains("imgmount d \"", confContent, StringComparison.Ordinal);
        Assert.Contains("\" -t cdrom", confContent, StringComparison.Ordinal);
        Assert.Contains("cd SUB", confContent, StringComparison.Ordinal);
        Assert.Contains("PLAY.BAT", confContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DosBoxChd_OnLinux_MixedModeDisc_LaunchesFromDataTrack()
    {
        if (OperatingSystem.IsWindows()) return;

        var chdPath = CreateMixedModeChd(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["GAME.EXE"] = new byte[1234] });

        var (strategy, launcher, _, _) = CreateStrategy();
        string? confContent = null;
        CaptureConf(launcher, content => confContent = content);

        await strategy.ExecuteAsync(CreateContext(chdPath), launcher.Object);

        Assert.NotNull(confContent);
        Assert.Contains("imgmount d \"", confContent, StringComparison.Ordinal);
        Assert.Contains("\" -t cdrom", confContent, StringComparison.Ordinal);
        Assert.Contains("GAME.EXE", confContent, StringComparison.Ordinal);
        Assert.EndsWith(".cue", ExtractImgmountPath(confContent), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DosBoxChd_OnLinux_NoExecutable_ReportsMissingFile()
    {
        if (OperatingSystem.IsWindows()) return;

        var chdPath = CreateChd(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["README.TXT"] = new byte[42] });

        var (strategy, launcher, messageBox, _) = CreateStrategy();

        await strategy.ExecuteAsync(CreateContext(chdPath), launcher.Object);

        messageBox.Verify(m => m.CouldNotFindAFileMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        launcher.Verify(l => l.LaunchRegularEmulatorAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
            It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task DosBoxChd_OnLinux_ConversionFailure_ShowsLaunchError()
    {
        if (OperatingSystem.IsWindows()) return;

        var converter = new Mock<IDiscConverter>();
        converter.Setup(c => c.ConvertChdToCueBinAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        converter.Setup(c => c.ConvertChdToIsoAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        var (strategy, launcher, messageBox, _) = CreateStrategy(converter.Object);
        var chdPath = Path.Combine(_root, "broken.chd");
        await File.WriteAllTextAsync(chdPath, "not a chd");

        await strategy.ExecuteAsync(CreateContext(chdPath), launcher.Object);

        messageBox.Verify(m => m.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(It.IsAny<string>()), Times.Once);
        messageBox.Verify(m => m.CouldNotFindAFileMessageBoxAsync(), Times.Never);
        launcher.Verify(l => l.LaunchRegularEmulatorAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
            It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()), Times.Never);
    }

    private static (DosBoxLaunchStrategy Strategy, Mock<ILauncherService> Launcher,
        Mock<IMessageBoxLibraryService> MessageBox, Mock<IMountChdFiles> MountChd) CreateStrategy(
            IDiscConverter? converter = null)
    {
        var messageBox = TestDependencies.MessageBox();
        var mountChd = new Mock<IMountChdFiles>();
        var launcher = new Mock<ILauncherService>();

        var strategy = new DosBoxLaunchStrategy(
            new Mock<IExtractionService>().Object,
            TestEnvironment.ConfigurationFromJson("{}"),
            messageBox.Object,
            mountChd.Object,
            new Mock<IMountIsoFiles>().Object,
            converter ?? new DiscConverter(TestDependencies.Logger().Object),
            TestDependencies.Logger().Object);

        return (strategy, launcher, messageBox, mountChd);
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

    private static LaunchContext CreateContext(string chdPath)
    {
        return new LaunchContext
        {
            FilePath = chdPath,
            ResolvedFilePath = chdPath,
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

    private string CreateChd(IReadOnlyDictionary<string, byte[]> files)
    {
        var isoPath = Path.Combine(_root, "disc.iso");
        File.WriteAllBytes(isoPath, TestIso9660ImageBuilder.Build(files));

        var chdPath = Path.Combine(_root, "disc.chd");
        ChdEncoder.EncodeCd(isoPath, chdPath);
        return chdPath;
    }

    private string CreateMixedModeChd(IReadOnlyDictionary<string, byte[]> files)
    {
        var dataPath = Path.Combine(_root, "data.bin");
        File.WriteAllBytes(dataPath, TestIso9660ImageBuilder.Build(files));

        var audioPath = Path.Combine(_root, "audio.bin");
        File.WriteAllBytes(audioPath, new byte[2352 * 4]);

        var cuePath = Path.Combine(_root, "mixed.cue");
        File.WriteAllText(cuePath, string.Join("\r\n",
            "FILE \"data.bin\" BINARY",
            "  TRACK 01 MODE1/2048",
            "    INDEX 01 00:00:00",
            "FILE \"audio.bin\" BINARY",
            "  TRACK 02 AUDIO",
            "    INDEX 01 00:00:00",
            ""));

        var chdPath = Path.Combine(_root, "mixed.chd");
        ChdEncoder.EncodeCd(cuePath, chdPath);
        return chdPath;
    }
}
