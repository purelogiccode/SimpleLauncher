using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Cross-platform regression tests for the ZIP launch fallback (LB-01): the path-traversal
///     validator must not simulate extraction under a Windows-only root, Windows-authored
///     entries (DIR\FILE.BIN) must keep their directory structure on Unix, and on platforms
///     without Dokan/SimpleZipDrive archives are extracted to a temp directory instead of
///     being mounted.
/// </summary>
public class MountZipFilesCrossPlatformTests
{
    private static MountZipFiles CreateMountZipFiles()
    {
        return new MountZipFiles(new ConfigurationBuilder().Build(), Log.Logger);
    }

    private static string CreateTestZip(params string[] entryNames)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sl-mountzip-{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(tempFile, ZipArchiveMode.Create);
        foreach (var entryName in entryNames)
        {
            var entry = archive.CreateEntry(entryName);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write($"content of {entryName}");
        }

        return tempFile;
    }

    [Fact]
    public void ValidateZipForPathTraversal_ArchiveWithBackslashEntries_DoesNotThrow()
    {
        // Before the fix this threw on Linux for every entry: "D:\MOCKROOT" is a relative
        // path on Unix, so the simulated-root check could never be satisfied there.
        var zipPath = CreateTestZip(@"PS3_GAME\USRDIR\EBOOT.BIN", @"PS3_GAME\PARAM.SFO");
        try
        {
            var exception = Record.Exception(() => CreateMountZipFiles().ValidateZipForPathTraversal(zipPath));
            Assert.Null(exception);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Fact]
    public void ValidateZipForPathTraversal_MixedSeparators_DoesNotThrow()
    {
        var zipPath = CreateTestZip("roms/game.bin", @"roms\game.cue", "readme.txt");
        try
        {
            var exception = Record.Exception(() => CreateMountZipFiles().ValidateZipForPathTraversal(zipPath));
            Assert.Null(exception);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Theory]
    [InlineData("roms/../evil.exe")]
    [InlineData("../evil.exe")]
    [InlineData("/evil.exe")]
    [InlineData(@"\evil.exe")]
    public void ValidateZipForPathTraversal_TraversalEntries_ThrowInvalidOperationException(string entryName)
    {
        var zipPath = CreateTestZip(entryName);
        try
        {
            Assert.Throws<InvalidOperationException>(() => CreateMountZipFiles().ValidateZipForPathTraversal(zipPath));
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    [Fact]
    public void ExtractArchiveToTempDirectory_BackslashEntries_KeepDirectoryStructure()
    {
        var zipPath = CreateTestZip(@"DIR\FILE.BIN", "sub/other.txt");
        var mountZipFiles = CreateMountZipFiles();
        var extractedDirectory = "";
        try
        {
            extractedDirectory = mountZipFiles.ExtractArchiveToTempDirectory(zipPath);

            var nestedBackslashFile = Path.Combine(extractedDirectory, "DIR", "FILE.BIN");
            var nestedSlashFile = Path.Combine(extractedDirectory, "sub", "other.txt");

            Assert.True(File.Exists(nestedBackslashFile));
            Assert.True(File.Exists(nestedSlashFile));
            Assert.Contains("content of", File.ReadAllText(nestedBackslashFile));

            if (!OperatingSystem.IsWindows())
            {
                // On Unix the raw backslash name would have been extracted as a flat file.
                Assert.False(File.Exists(Path.Combine(extractedDirectory, @"DIR\FILE.BIN")));
            }
        }
        finally
        {
            File.Delete(zipPath);
            mountZipFiles.TryDeleteDirectory(extractedDirectory);
        }
    }

    [Fact]
    public async Task MountZipFileAndSearchForFileToLoadAsync_OnUnix_ExtractsToTempDirectoryAndLaunchesFoundFile()
    {
        if (OperatingSystem.IsWindows())
        {
            // The mount path requires SimpleZipDrive/Dokan and is covered by the WPF suite;
            // the extraction fallback added for LB-01 only runs on Unix.
            return;
        }

        var zipPath = CreateTestZip("000D0000/game.xex", "readme.txt");
        string? launchedPath = null;

        var launcher = new Mock<ILauncherService>();
        launcher
            .Setup(l => l.LaunchRegularEmulatorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISystemManager>(), It.IsAny<Emulator>(),
                It.IsAny<string>(), It.IsAny<IWindowContext>(), It.IsAny<ILoadingState?>(), It.IsAny<string?>()))
            .Callback<string, string, ISystemManager, Emulator, string, IWindowContext, ILoadingState?, string?>(
                (path, _, _, _, _, _, _, _) => launchedPath = path)
            .Returns(Task.CompletedTask);

        var messageBox = new Mock<IMessageBoxLibraryService>();

        try
        {
            await CreateMountZipFiles().MountZipFileAndSearchForFileToLoadAsync(
                zipPath,
                "Xbox 360 XBLA",
                "Xenia",
                Mock.Of<ISystemManager>(),
                new Emulator
                {
                    EmulatorName = "Xenia",
                    EmulatorLocation = "/usr/bin/xenia",
                    EmulatorParameters = "",
                    ImagePackDownloadLink = "",
                    ImagePackDownloadLink2 = "",
                    ImagePackDownloadLink3 = "",
                    ImagePackDownloadLink4 = "",
                    ImagePackDownloadLink5 = "",
                    ImagePackDownloadExtractPath = ""
                },
                "",
                new Mock<IWindowContext>().Object,
                null,
                launcher.Object,
                Log.Logger,
                messageBox.Object);

            Assert.NotNull(launchedPath);
            Assert.EndsWith("game.xex", launchedPath);

            // The extraction directory is removed once the emulator has exited.
            Assert.False(Directory.Exists(Path.GetDirectoryName(launchedPath)));

            messageBox.Verify(m => m.ThereWasAnErrorMountingTheFileMessageBoxAsync(It.IsAny<int?>()), Times.Never);
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}
