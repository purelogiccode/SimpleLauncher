using Moq;
using SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     LB-19 regression test: the temporary files of a converted CHD must all be deleted —
///     split-bin/GD-ROM conversions produce per-track .bin files besides the CUE/BIN pair.
/// </summary>
public class ChdMountStrategyCleanupTests
{
    [Fact]
    public void CleanupConvertedFiles_DeletesEveryFileWithTheConversionBaseName()
    {
        var folder = Directory.CreateTempSubdirectory("sl-chd-cleanup-");
        try
        {
            var baseName = Guid.NewGuid().ToString("N");
            var cuePath = Path.Combine(folder.FullName, baseName + ".cue");
            var binPath = Path.Combine(folder.FullName, baseName + ".bin");
            var track2 = Path.Combine(folder.FullName, baseName + " (Track 2).bin");
            var track3 = Path.Combine(folder.FullName, baseName + " (Track 3).bin");
            var unrelated = Path.Combine(folder.FullName, "other-game.cue");

            foreach (var file in new[] { cuePath, binPath, track2, track3, unrelated })
                File.WriteAllText(file, "x");

            var strategy = new ChdMountStrategy(
                TestEnvironment.ConfigurationFromJson("{}"),
                TestDependencies.MessageBox().Object,
                new Mock<IMountChdFiles>().Object,
                new Mock<IDiscConverter>().Object,
                TestDependencies.Logger().Object);

            strategy.CleanupConvertedFiles(cuePath);

            Assert.False(File.Exists(cuePath));
            Assert.False(File.Exists(binPath));
            Assert.False(File.Exists(track2));
            Assert.False(File.Exists(track3));
            Assert.True(File.Exists(unrelated), "files not belonging to the conversion must stay");
        }
        finally
        {
            Directory.Delete(folder.FullName, true);
        }
    }
}
