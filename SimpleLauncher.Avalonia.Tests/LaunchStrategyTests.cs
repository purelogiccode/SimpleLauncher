using Moq;
using SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.GameLauncher.Strategies;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the Phase 10 launch strategy port — dispatch (IsMatch) rules for every
///     ported strategy and the global priority ordering (ascending, Default last).
///     The Core strategies (Default/ZipMount/XisoMount) are asserted for parity only.
/// </summary>
public class LaunchStrategyTests
{
    private static LaunchContext Context(string filePath, string emulatorName, string? emulatorLocation = null,
        string systemName = "System")
    {
        return new LaunchContext
        {
            FilePath = filePath,
            ResolvedFilePath = filePath,
            EmulatorName = emulatorName,
            SystemName = systemName,
            SystemManagerService = new SystemManagerConfig { SystemName = systemName },
            EmulatorManager = new Emulator
            {
                EmulatorName = emulatorName,
                EmulatorLocation = emulatorLocation ?? ""
            }
        };
    }

    // ── PbpToCueStrategy ──

    [Fact]
    public void PbpToCue_MatchesPbpWithMednafen()
    {
        var strategy = new PbpToCueStrategy(
            TestDependencies.MessageBox().Object, TestDependencies.Logger().Object,
            new Mock<IDiscConverter>().Object, TestEnvironment.ConfigurationFromJson("{}"));

        Assert.True(strategy.IsMatch(Context(@"C:\games\game.pbp", "Mednafen")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.pbp", "DuckStation", @"C:\emu\mednafen.exe")));
    }

    [Fact]
    public void PbpToCue_DoesNotMatchNonMednafenOrNonPbp()
    {
        var strategy = new PbpToCueStrategy(
            TestDependencies.MessageBox().Object, TestDependencies.Logger().Object,
            new Mock<IDiscConverter>().Object, TestEnvironment.ConfigurationFromJson("{}"));

        Assert.False(strategy.IsMatch(Context(@"C:\games\game.pbp", "DuckStation")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\game.iso", "Mednafen")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\game.pbp", "")));
    }

    // ── ChdToCueStrategy ──

    [Fact]
    public void ChdToCue_MatchesChdWith4DoOrRaine()
    {
        var strategy = new ChdToCueStrategy(
            TestDependencies.MessageBox().Object, TestDependencies.Logger().Object,
            new Mock<IDiscConverter>().Object, TestEnvironment.ConfigurationFromJson("{}"));

        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "4DO")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "Raine")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "Other", @"C:\emu\4do.exe")));
    }

    [Fact]
    public void ChdToCue_DoesNotMatchOtherEmulatorsOrExtensions()
    {
        var strategy = new ChdToCueStrategy(
            TestDependencies.MessageBox().Object, TestDependencies.Logger().Object,
            new Mock<IDiscConverter>().Object, TestEnvironment.ConfigurationFromJson("{}"));

        Assert.False(strategy.IsMatch(Context(@"C:\games\game.chd", "RetroArch")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\game.cue", "4DO")));
    }

    // ── ChdMountStrategy ──

    [Fact]
    public void ChdMount_MatchesMountCapableEmulators()
    {
        var strategy = new ChdMountStrategy(
            TestEnvironment.ConfigurationFromJson("{}"), TestDependencies.MessageBox().Object,
            new Mock<IMountChdFiles>().Object, new Mock<IDiscConverter>().Object, TestDependencies.Logger().Object);

        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "RPCS3")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "Xenia")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "4DO")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "Raine")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "Genesis Plus GX")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "Gens")));
    }

    [Fact]
    public void ChdMount_ExcludesRetroArchDosBoxAndNonChd()
    {
        var strategy = new ChdMountStrategy(
            TestEnvironment.ConfigurationFromJson("{}"), TestDependencies.MessageBox().Object,
            new Mock<IMountChdFiles>().Object, new Mock<IDiscConverter>().Object, TestDependencies.Logger().Object);

        Assert.False(strategy.IsMatch(Context(@"C:\games\game.chd", "RetroArch")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\game.chd", "DOSBox")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\game.chd", "UnknownEmulator")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\game.iso", "RPCS3")));
    }

    // ── DosBoxLaunchStrategy ──

    [Fact]
    public void DosBox_MatchesDosBoxEmulatorsWithSupportedFormats()
    {
        var strategy = new DosBoxLaunchStrategy(
            new Mock<IExtractionService>().Object, TestEnvironment.ConfigurationFromJson("{}"),
            TestDependencies.MessageBox().Object, new Mock<IMountChdFiles>().Object,
            new Mock<IMountIsoFiles>().Object, TestDependencies.Logger().Object);

        Assert.True(strategy.IsMatch(Context(@"C:\games\game.zip", "DOSBox")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.7z", "DOSBox-X")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.rar", "DOSBox Staging")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.iso", "DOSBox")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.chd", "DOSBox")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\game.zip", "Other", @"C:\emu\dosbox.exe")));
    }

    [Fact]
    public void DosBox_DoesNotMatchNonDosBoxEmulatorsOrUnsupportedFormats()
    {
        var strategy = new DosBoxLaunchStrategy(
            new Mock<IExtractionService>().Object, TestEnvironment.ConfigurationFromJson("{}"),
            TestDependencies.MessageBox().Object, new Mock<IMountChdFiles>().Object,
            new Mock<IMountIsoFiles>().Object, TestDependencies.Logger().Object);

        Assert.False(strategy.IsMatch(Context(@"C:\games\game.zip", "RetroArch")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\game.cue", "DOSBox")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\game.zip", "")));
    }

    [Fact]
    public void DosBox_MatchesDirectoriesWithTheDosBoxEmulator()
    {
        var strategy = new DosBoxLaunchStrategy(
            new Mock<IExtractionService>().Object, TestEnvironment.ConfigurationFromJson("{}"),
            TestDependencies.MessageBox().Object, new Mock<IMountChdFiles>().Object,
            new Mock<IMountIsoFiles>().Object, TestDependencies.Logger().Object);

        using var tempDir = new TempDirectory();
        Assert.True(strategy.IsMatch(Context(tempDir.Path, "DOSBox")));
    }

    // ── CommanderGeniusLaunchStrategy ──

    [Fact]
    public void CommanderGenius_MatchesArchivesWithTheEmulator()
    {
        var strategy = new CommanderGeniusLaunchStrategy(
            new Mock<IExtractionService>().Object, TestEnvironment.ConfigurationFromJson("{}"),
            TestDependencies.MessageBox().Object, TestDependencies.Logger().Object);

        Assert.True(strategy.IsMatch(Context(@"C:\games\keen4.zip", "Commander Genius")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\keen4.7z", "Commander Genius")));
        Assert.True(strategy.IsMatch(Context(@"C:\games\keen4.rar", "Commander Genius")));
    }

    [Fact]
    public void CommanderGenius_DoesNotMatchOtherEmulatorsOrFormats()
    {
        var strategy = new CommanderGeniusLaunchStrategy(
            new Mock<IExtractionService>().Object, TestEnvironment.ConfigurationFromJson("{}"),
            TestDependencies.MessageBox().Object, TestDependencies.Logger().Object);

        Assert.False(strategy.IsMatch(Context(@"C:\games\keen4.iso", "Commander Genius")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\keen4.zip", "DOSBox")));
        Assert.False(strategy.IsMatch(Context(@"C:\games\keen4.exe", "Commander Genius")));
    }

    // ── Priority ordering (full 8-strategy pipeline, same as the WPF app) ──

    [Fact]
    public void PipelinePriorities_MatchTheWpfOrdering()
    {
        var config = TestEnvironment.ConfigurationFromJson("{}");
        var messageBox = TestDependencies.MessageBox().Object;
        var logger = TestDependencies.Logger().Object;
        var converter = new Mock<IDiscConverter>().Object;
        var extraction = new Mock<IExtractionService>().Object;
        var chd = new Mock<IMountChdFiles>().Object;
        var iso = new Mock<IMountIsoFiles>().Object;

        var strategies = new List<ILaunchStrategy>
        {
            new DefaultLaunchStrategy(), // 999
            new ZipMountStrategy(config, logger, messageBox, new Mock<IMountZipFiles>().Object), // 30
            new XisoMountStrategy(logger, messageBox, new Mock<IMountXisoFiles>().Object), // 20
            new ChdMountStrategy(config, messageBox, chd, converter, logger), // 10
            new PbpToCueStrategy(messageBox, logger, converter, config), // 15
            new CommanderGeniusLaunchStrategy(extraction, config, messageBox, logger), // 20
            new ChdToCueStrategy(messageBox, logger, converter, config), // 25
            new DosBoxLaunchStrategy(extraction, config, messageBox, chd, iso, logger) // 25
        };

        var ordered = strategies.OrderBy(s => s.Priority).ToList();

        Assert.Equal(8, ordered.Count);
        Assert.IsType<ChdMountStrategy>(ordered[0]);
        Assert.IsType<PbpToCueStrategy>(ordered[1]);
        Assert.IsType<XisoMountStrategy>(ordered[2]);
        Assert.IsType<CommanderGeniusLaunchStrategy>(ordered[3]);
        Assert.IsType<ChdToCueStrategy>(ordered[4]); // 25, stable with DosBox
        Assert.IsType<DosBoxLaunchStrategy>(ordered[5]);
        Assert.IsType<ZipMountStrategy>(ordered[6]);
        Assert.IsType<DefaultLaunchStrategy>(ordered[7]);
        Assert.Equal(999, ordered[^1].Priority);
    }

    [Fact]
    public void StrategySelection_MatchesTheWpfFirstMatchBehavior()
    {
        var config = TestEnvironment.ConfigurationFromJson("{}");
        var messageBox = TestDependencies.MessageBox().Object;
        var logger = TestDependencies.Logger().Object;
        var converter = new Mock<IDiscConverter>().Object;
        var extraction = new Mock<IExtractionService>().Object;
        var chd = new Mock<IMountChdFiles>().Object;
        var iso = new Mock<IMountIsoFiles>().Object;

        var strategies = new List<ILaunchStrategy>
        {
            new DefaultLaunchStrategy(),
            new ZipMountStrategy(config, logger, messageBox, new Mock<IMountZipFiles>().Object),
            new XisoMountStrategy(logger, messageBox, new Mock<IMountXisoFiles>().Object),
            new ChdMountStrategy(config, messageBox, chd, converter, logger),
            new PbpToCueStrategy(messageBox, logger, converter, config),
            new CommanderGeniusLaunchStrategy(extraction, config, messageBox, logger),
            new ChdToCueStrategy(messageBox, logger, converter, config),
            new DosBoxLaunchStrategy(extraction, config, messageBox, chd, iso, logger)
        }.OrderBy(s => s.Priority).ToList();

        // .pbp + Mednafen → PbpToCue (before Default/others)
        Assert.IsType<PbpToCueStrategy>(strategies.First(s => s.IsMatch(Context(@"C:\g.pbp", "Mednafen"))));
        // .chd + RPCS3 → ChdMount (priority 10 beats ChdToCue)
        Assert.IsType<ChdMountStrategy>(strategies.First(s => s.IsMatch(Context(@"C:\g.chd", "RPCS3"))));
        // .chd + RetroArch → ChdMount/ChdToCue/DosBox all exclude it → Default
        Assert.IsType<DefaultLaunchStrategy>(strategies.First(s => s.IsMatch(Context(@"C:\g.chd", "RetroArch"))));
        // .zip + Commander Genius → CommanderGenius (20 beats ZipMount 30)
        Assert.IsType<CommanderGeniusLaunchStrategy>(strategies.First(s =>
            s.IsMatch(Context(@"C:\g.zip", "Commander Genius"))));
        // .zip + DOSBox → DosBox (25 beats ZipMount 30)
        Assert.IsType<DosBoxLaunchStrategy>(strategies.First(s => s.IsMatch(Context(@"C:\g.zip", "DOSBox"))));
        // .zip + ScummVM system → ZipMount (30; DosBox excludes non-DOSBox emulators)
        Assert.IsType<ZipMountStrategy>(strategies.First(s =>
            s.IsMatch(Context(@"C:\g.zip", "RetroArch", systemName: "ScummVM"))));
        // .iso + Cxbx → XisoMount (20)
        Assert.IsType<XisoMountStrategy>(strategies.First(s => s.IsMatch(Context(@"C:\g.iso", "Cxbx-Reloaded"))));
        // .cue + DuckStation → Default (no strategy matches)
        Assert.IsType<DefaultLaunchStrategy>(strategies.First(s => s.IsMatch(Context(@"C:\g.cue", "DuckStation"))));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sl_av_dos_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}