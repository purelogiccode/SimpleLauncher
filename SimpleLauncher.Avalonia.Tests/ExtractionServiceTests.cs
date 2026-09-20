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

    [Fact]
    public async Task ExtractToTempAndGetLaunchFileAsync_FindsUpperCaseNamesForLowerCaseFormats()
    {
        var folder = Directory.CreateTempSubdirectory("sl-extract-case-");
        var tempDirs = new List<string>();
        try
        {
            var zipPath = Path.Combine(folder.FullName, "disc.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var entryName in new[] { "DISC.CUE", "GAME.EXE", "README.TXT" })
                {
                    using var stream = archive.CreateEntry(entryName).Open();
                    using var writer = new StreamWriter(stream);
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
}

