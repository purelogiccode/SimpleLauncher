using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using SimpleLauncher.Core.Services.ExtractFiles;
using Xunit.Sdk;

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
    [InlineData("redream.x86_64-linux-v1.5.0.tar.gz", true)]
    [InlineData("REDREAM.TAR.GZ", true)]
    [InlineData("redream.tgz", true)]
    [InlineData("dosbox-staging-linux-x86_64-v0.83.0.tar.xz", true)]
    [InlineData("ymir-linux-x86_64-AVX2-v0.3.3.tar.xz", true)]
    [InlineData("ymir.txz", true)]
    [InlineData("azahar.AppImage", false)]
    [InlineData("retroarch", false)]
    public void IsSupportedArchivePath_ClassifiesArchiveExtensions(string fileName, bool expected)
    {
        // Bug #67537: non-archive downloads (Linux AppImages) must not be routed into
        // the extraction methods. The Linux Easy Mode manifest ships some emulators as
        // .tar.gz (Redream) and .tar.xz (DOSBox Staging, Ymir), so both must be accepted.
        Assert.Equal(expected, ExtractionService.IsSupportedArchivePath(fileName));
    }

    [Fact]
    public async Task ExtractToFolderAsync_ExtractsTarGzAndRestoresUnixExecuteBits()
    {
        var folder = Directory.CreateTempSubdirectory("sl-extract-targz-");
        try
        {
            // The Linux Easy Mode manifest ships Redream as a tar.gz whose single entry
            // (`redream`, mode 0755) must be installed executable, or the launcher fails
            // with EACCES when it tries to start it.
            var tarGzPath = Path.Combine(folder.FullName, "redream.x86_64-linux-v1.5.0.tar.gz");
            await using (var fileStream = File.Create(tarGzPath))
            await using (var gzip = new GZipStream(fileStream, CompressionLevel.SmallestSize))
            await using (var writer = new TarWriter(gzip, TarEntryFormat.Pax))
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, "redream")
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes("#!/bin/sh\n"))
                };
                if (!OperatingSystem.IsWindows())
                {
                    entry.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                 UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                 UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                }

                writer.WriteEntry(entry);
            }

            var service = new ExtractionService(
                TestDependencies.MessageBox().Object, TestDependencies.Logger().Object);

            var destination = Path.Combine(folder.FullName, "out");
            var success = await service.ExtractToFolderAsync(tarGzPath, destination);

            Assert.True(success);
            var extracted = Path.Combine(destination, "redream");
            Assert.True(File.Exists(extracted));
            Assert.Equal("#!/bin/sh\n", await File.ReadAllTextAsync(extracted));

            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(extracted);
                Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
                Assert.True(mode.HasFlag(UnixFileMode.GroupExecute));
                Assert.True(mode.HasFlag(UnixFileMode.OtherExecute));
            }
        }
        finally
        {
            Directory.Delete(folder.FullName, true);
        }
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

    [Fact]
    public async Task ExtractToFolderAsync_ExtractsTarXzAndRestoresUnixExecuteBits()
    {
        var sevenZip = FindBundledSevenZip();
        if (sevenZip is null) throw SkipException.ForSkip("Bundled 7-Zip tool not found.");
        EnsureExecutable(sevenZip);

        var folder = Directory.CreateTempSubdirectory("sl-extract-tarxz-");
        try
        {
            // The Linux Easy Mode manifest ships DOSBox Staging and Ymir as .tar.xz; both
            // must install with the binary executable (the tar carries Unix modes).
            var source = Path.Combine(folder.FullName, "source");
            Directory.CreateDirectory(source);
            var payload = Path.Combine(source, "ymir-sdl3");
            await File.WriteAllTextAsync(payload, "#!/bin/sh\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(payload, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                              UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                              UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            var archivePath = Path.Combine(folder.FullName, "ymir-linux-x86_64-AVX2-v0.3.3.tar.xz");
            // 7-Zip's -txz alone compresses a single file into a raw xz stream; a real
            // tar.xz needs the tar container first, then xz compression of that tar.
            var tarPath = Path.Combine(folder.FullName, "fixture.tar");
            Assert.True(await RunSevenZipAsync(sevenZip, source, "a", "-ttar", tarPath, "ymir-sdl3"),
                "7-Zip could not create the tar fixture.");
            Assert.True(await RunSevenZipAsync(sevenZip, folder.FullName, "a", "-txz", archivePath, "fixture.tar"),
                "7-Zip could not create the tar.xz fixture.");

            var service = new ExtractionService(
                TestDependencies.MessageBox().Object, TestDependencies.Logger().Object);
            var destination = Path.Combine(folder.FullName, "out");

            Assert.True(await service.ExtractToFolderAsync(archivePath, destination));
            var extracted = Path.Combine(destination, "ymir-sdl3");
            Assert.True(File.Exists(extracted));
            Assert.Equal("#!/bin/sh\n", await File.ReadAllTextAsync(extracted));
            if (!OperatingSystem.IsWindows())
                Assert.True(File.GetUnixFileMode(extracted).HasFlag(UnixFileMode.UserExecute));
        }
        finally
        {
            Directory.Delete(folder.FullName, true);
        }
    }

    [Fact]
    public async Task ExtractToFolderAsync_ExtractsSolidSevenZipWithAllEntries()
    {
        var sevenZip = FindBundledSevenZip();
        if (sevenZip is null) throw SkipException.ForSkip("Bundled 7-Zip tool not found.");
        EnsureExecutable(sevenZip);

        var folder = Directory.CreateTempSubdirectory("sl-extract-solid-");
        try
        {
            // Solid archives must be read in one forward pass: random access re-decompresses
            // the solid block per entry, which made the 19,797-file RetroArch bundle take
            // hours instead of seconds.
            var source = Path.Combine(folder.FullName, "source");
            Directory.CreateDirectory(source);
            for (var i = 1; i <= 5; i++)
                await File.WriteAllTextAsync(Path.Combine(source, $"file{i}.txt"), new string((char)('a' + i), 4096));

            var archivePath = Path.Combine(folder.FullName, "solid.7z");
            Assert.True(await RunSevenZipAsync(sevenZip, source, "a", "-t7z", "-ms=on", archivePath, "."),
                "7-Zip could not create the solid 7z fixture.");

            var service = new ExtractionService(
                TestDependencies.MessageBox().Object, TestDependencies.Logger().Object);
            var destination = Path.Combine(folder.FullName, "out");

            Assert.True(await service.ExtractToFolderAsync(archivePath, destination));
            for (var i = 1; i <= 5; i++)
            {
                var extracted = Path.Combine(destination, $"file{i}.txt");
                Assert.True(File.Exists(extracted), $"Missing {extracted}");
                Assert.Equal(new string((char)('a' + i), 4096), await File.ReadAllTextAsync(extracted));
            }
        }
        finally
        {
            Directory.Delete(folder.FullName, true);
        }
    }

    private static string? FindBundledSevenZip()
    {
        var name = ExtractionService.GetSevenZipExecutableName(RuntimeInformation.ProcessArchitecture,
            OperatingSystem.IsWindows());
        var path = Path.Combine(AppContext.BaseDirectory, "tools", "SevenZip", name);
        return File.Exists(path) ? path : null;
    }

    private static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsWindows()) ExtractionService.EnsureExecuteBits(path);
    }

    private static async Task<bool> RunSevenZipAsync(string sevenZipPath, string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sevenZipPath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        if (process is null) return false;
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }
}

