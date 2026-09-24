using System.IO.Compression;
using System.Runtime.InteropServices;
using SimpleLauncher.Core.Services.ExtractFiles;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the 7-Zip fallback executable selection in <see cref="ExtractionService" /> —
///     Windows bundles 7za.exe, Linux/macOS bundle the extension-less 7zz binaries — and for the
///     case-insensitive post-extraction game-file discovery (LB-07).
/// </summary>
public class ExtractionServiceTests
{
    [Theory]
    [InlineData(Architecture.X64, true, "7za.exe")]
    [InlineData(Architecture.Arm64, true, "7za_arm64.exe")]
    [InlineData(Architecture.X64, false, "7zz")]
    [InlineData(Architecture.Arm64, false, "7zz_arm64")]
    public void GetSevenZipExecutableName_PicksBinaryForPlatform(
        Architecture architecture, bool isWindows, string expected)
    {
        Assert.Equal(expected, ExtractionService.GetSevenZipExecutableName(architecture, isWindows));
    }

    [Theory]
    [InlineData("game.zip", true)]
    [InlineData("GAME.ZIP", true)]
    [InlineData("game.7z", true)]
    [InlineData("game.rar", true)]
    [InlineData("azahar.AppImage", false)]
    [InlineData("dosbox-staging-linux-x86_64-v0.83.0.tar.xz", false)]
    [InlineData("retroarch", false)]
    public void IsSupportedArchivePath_ClassifiesArchiveExtensions(string fileName, bool expected)
    {
        // Bug #67537: non-archive downloads (Linux AppImages) must not be routed into
        // the extraction methods.
        Assert.Equal(expected, ExtractionService.IsSupportedArchivePath(fileName));
    }

    [Fact]
    public async Task ExtractToTempAndGetLaunchFileAsync_FindsUpperCaseNamesForLowerCaseFormats()
    {
        var folder = Directory.CreateTempSubdirectory("sl-extract-case-");
        var tempDirs = new List<string>();
        try
        {
            var zipPath = Path.Combine(folder.FullName, "disc.zip");
            await using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var entryName in new[] { "DISC.CUE", "GAME.EXE", "README.TXT" })
                {
                    await using var stream = archive.CreateEntry(entryName).Open();
                    await using var writer = new StreamWriter(stream);
                    writer.Write($"content of {entryName}");
                }
            }

            var service = new ExtractionService(
                TestDependencies.MessageBox().Object, TestDependencies.Logger().Object);

            // Scene archives use upper-case names (DISC.CUE, GAME.EXE) while system configs
            // store lower-case launch extensions; the search must match both on Linux.
            var (cueFile, cueTempDir) = await service.ExtractToTempAndGetLaunchFileAsync(zipPath, [".cue"]);
            var (exeFile, exeTempDir) = await service.ExtractToTempAndGetLaunchFileAsync(zipPath, [".exe"]);
            if (cueTempDir is not null) tempDirs.Add(cueTempDir);
            if (exeTempDir is not null) tempDirs.Add(exeTempDir);

            Assert.NotNull(cueFile);
            Assert.True(cueFile.EndsWith("DISC.CUE", StringComparison.Ordinal), cueFile);
            Assert.NotNull(exeFile);
            Assert.True(exeFile.EndsWith("GAME.EXE", StringComparison.Ordinal), exeFile);
        }
        finally
        {
            foreach (var tempDir in tempDirs)
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }

            Directory.Delete(folder.FullName, true);
        }
    }

    [Fact]
    public async Task ExtractToTempFolderAsync_NormalizesWindowsBackslashEntryNames()
    {
        var folder = Directory.CreateTempSubdirectory("sl-extract-backslash-");
        string? tempDir = null;
        try
        {
            var zipPath = Path.Combine(folder.FullName, "game.zip");
            await using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                await using (var stream = archive.CreateEntry(@"DATA\GAME.BIN").Open())
                {
                    stream.WriteByte(1);
                }

                await using (var stream = archive.CreateEntry("README.TXT").Open())
                {
                    stream.WriteByte(2);
                }
            }

            var service = new ExtractionService(
                TestDependencies.MessageBox().Object, TestDependencies.Logger().Object);

            // LB-16: Windows-authored archives store "DIR\FILE.BIN"; on Linux the entry used to
            // extract as one flat file literally named "DATA\GAME.BIN".
            var (launchFile, extractedTempDir) = await service.ExtractToTempAndGetLaunchFileAsync(zipPath, [".bin"]);
            tempDir = extractedTempDir;

            Assert.NotNull(launchFile);
            Assert.Equal(Path.Combine("DATA", "GAME.BIN"), Path.GetRelativePath(tempDir!, launchFile));
            Assert.True(File.Exists(Path.Combine(tempDir!, "DATA", "GAME.BIN")));

            // On Unix the un-normalized entry would have produced a flat file whose name
            // contains a literal backslash.
            if (!OperatingSystem.IsWindows())
                Assert.False(File.Exists(Path.Combine(tempDir!, @"DATA\GAME.BIN")));
        }
        finally
        {
            if (tempDir is not null)
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }

            Directory.Delete(folder.FullName, true);
        }
    }

    [Fact]
    public void NormalizeArchiveEntryName_ReplacesBackslashesWithThePlatformSeparator()
    {
        Assert.Equal(Path.Combine("DIR", "FILE.BIN"), ExtractionService.NormalizeArchiveEntryName(@"DIR\FILE.BIN"));
        Assert.Equal(Path.Combine("A", "B", "C.bin"), ExtractionService.NormalizeArchiveEntryName(@"A\B\C.bin"));
        Assert.Equal("GAME.EXE", ExtractionService.NormalizeArchiveEntryName("GAME.EXE"));
    }

    [Fact]
    public void EnsureExecuteBits_AddsTheExecuteBitsOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;

        // LB-15: git stores tools/SevenZip/7zz as 100644; the extraction fallback must fix
        // the mode before starting the process or it fails with EACCES.
        var file = Path.Combine(Path.GetTempPath(), $"sl-execbit-{Guid.NewGuid():N}");
        File.WriteAllText(file, "#!/bin/sh\n");
        try
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            ExtractionService.EnsureExecuteBits(file);

            var mode = File.GetUnixFileMode(file);
            Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
            Assert.True(mode.HasFlag(UnixFileMode.GroupExecute));
            Assert.True(mode.HasFlag(UnixFileMode.OtherExecute));
        }
        finally
        {
            File.Delete(file);
        }
    }
}

