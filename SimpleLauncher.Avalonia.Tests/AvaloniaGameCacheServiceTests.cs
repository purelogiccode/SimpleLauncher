using Moq;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="AvaloniaGameCacheService" /> and
///     <see cref="AvaloniaGameFileLoadingOrchestrator" /> (Phase 3) — per-system caching,
///     scan-once semantics, invalidation, and tolerant folder enumeration.
/// </summary>
public class AvaloniaGameCacheServiceTests
{
    private static SystemManagerConfig System(string name, params string[] folders)
    {
        return new SystemManagerConfig
        {
            SystemName = name,
            SystemFolders = [.. folders],
            FileFormatsToSearch = [".zip", ".iso"]
        };
    }

    [Fact]
    public void GetCachedFiles_ReturnsNull_WhenNotCached()
    {
        var cache = new AvaloniaGameCacheService();

        Assert.Null(cache.GetCachedFiles("SNES"));
        Assert.False(cache.IsPopulated("SNES"));
    }

    [Fact]
    public void SetCachedFiles_ThenGet_ReturnsSnapshot()
    {
        var cache = new AvaloniaGameCacheService();
        cache.SetCachedFiles("SNES", ["a.zip", "b.zip"]);

        var snapshot = cache.GetCachedFiles("SNES");
        snapshot!.Add("c.zip");

        Assert.Equal(2, cache.GetCachedFiles("SNES")!.Count);
        Assert.True(cache.IsPopulated("snes")); // case-insensitive
    }

    [Fact]
    public void GetCachedOrScan_OnlyInvokesFactoryOnce()
    {
        var cache = new AvaloniaGameCacheService();
        var system = System("NES", @"C:\roms\nes");
        var scanCount = 0;

        var first = cache.GetCachedOrScan(system, _ =>
        {
            scanCount++;
            return ["mario.zip"];
        });
        var second = cache.GetCachedOrScan(system, _ =>
        {
            scanCount++;
            return ["mario.zip", "zelda.zip"];
        });

        Assert.Equal(1, scanCount);
        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, cache.CachedSystemCount);
    }

    [Fact]
    public void Invalidate_ForcesRescan()
    {
        var cache = new AvaloniaGameCacheService();
        var system = System("NES", @"C:\roms\nes");
        var scanCount = 0;

        cache.GetCachedOrScan(system, _ =>
        {
            scanCount++;
            return ["a.zip"];
        });
        cache.Invalidate("NES");
        var after = cache.GetCachedOrScan(system, _ =>
        {
            scanCount++;
            return ["a.zip", "b.zip"];
        });

        Assert.Equal(2, scanCount);
        Assert.Equal(2, after.Count);
        Assert.True(cache.IsPopulated("NES")); // re-populated by the rescan
    }

    [Fact]
    public void Clear_EmptiesAllSystems()
    {
        var cache = new AvaloniaGameCacheService();
        cache.SetCachedFiles("NES", ["a.zip"]);
        cache.SetCachedFiles("SNES", ["b.zip"]);

        cache.Clear();

        Assert.Equal(0, cache.CachedSystemCount);
        Assert.Null(cache.GetCachedFiles("NES"));
    }

    [Fact]
    public async Task GetCachedOrScan_DoesNotBlockReadersDuringScan()
    {
        // AV-19: disk enumeration must run outside the cache lock — a reader
        // probing an unrelated system must return while a scan is in flight.
        var cache = new AvaloniaGameCacheService();
        using var scanStarted = new ManualResetEventSlim(false);
        using var releaseScan = new ManualResetEventSlim(false);
        var system = System("NES", @"C:\roms\nes");

        var scanTask = Task.Run(() => cache.GetCachedOrScan(system, _ =>
        {
            scanStarted.Set();
            releaseScan.Wait(TimeSpan.FromSeconds(30));
            return (IEnumerable<string>)["mario.zip"];
        }));

        Assert.True(await Task.Run(() => scanStarted.Wait(TimeSpan.FromSeconds(30))));

        // Unrelated readers must not be blocked by the in-flight scan.
        await Task.Run(() =>
        {
            Assert.Null(cache.GetCachedFiles("SNES"));
            Assert.False(cache.IsPopulated("SNES"));
            Assert.Equal(0, cache.CachedSystemCount);
        }).WaitAsync(TimeSpan.FromSeconds(10));

        releaseScan.Set();
        var result = await scanTask.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(result);
    }

    // ── Orchestrator ──

    [Fact]
    public void GetGameFiles_ScansFolderWithExtensionFilter()
    {
        using var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, "game.zip"), "x");
        File.WriteAllText(Path.Combine(tempDir, "disc.iso"), "x");
        File.WriteAllText(Path.Combine(tempDir, "notes.txt"), "x");

        var system = System("Arcade", tempDir);
        var orchestrator =
            new AvaloniaGameFileLoadingOrchestrator(new AvaloniaGameCacheService(), new Mock<ILogger>().Object);

        var files = orchestrator.GetGameFiles(system);

        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.EndsWith("game.zip", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("disc.iso", StringComparison.Ordinal));
    }

    [Fact]
    public void GetGameFiles_RecursesIntoSubfolders()
    {
        using var tempDir = CreateTempDir();
        var sub = Path.Combine(tempDir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "nested.zip"), "x");

        var system = System("Arcade", tempDir);
        var orchestrator =
            new AvaloniaGameFileLoadingOrchestrator(new AvaloniaGameCacheService(), new Mock<ILogger>().Object);

        var files = orchestrator.GetGameFiles(system);

        Assert.Single(files);
        Assert.Contains(files, f => f.EndsWith("nested.zip", StringComparison.Ordinal));
    }

    [Fact]
    public void GetGameFiles_SkipsMissingAndInaccessibleFolders()
    {
        var system = System("Arcade", @"C:\does\not\exist", @"Z:\also\missing");
        var orchestrator =
            new AvaloniaGameFileLoadingOrchestrator(new AvaloniaGameCacheService(), new Mock<ILogger>().Object);

        var files = orchestrator.GetGameFiles(system);

        Assert.Empty(files); // no exception, no files
    }

    [Fact]
    public void InvalidateSystem_RefreshesCachedFiles()
    {
        using var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, "one.zip"), "x");

        var system = System("Arcade", tempDir);
        var orchestrator =
            new AvaloniaGameFileLoadingOrchestrator(new AvaloniaGameCacheService(), new Mock<ILogger>().Object);

        Assert.Single(orchestrator.GetGameFiles(system));

        // New file appears on disk; the cache is stale until invalidated
        File.WriteAllText(Path.Combine(tempDir, "two.zip"), "x");
        Assert.Single(orchestrator.GetGameFiles(system));

        orchestrator.InvalidateSystem(system.SystemName);
        Assert.Equal(2, orchestrator.GetGameFiles(system).Count);
    }

    [Fact]
    public void ComputeSystemCounts_ReturnsCountsPerSystem()
    {
        using var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, "a.zip"), "x");
        File.WriteAllText(Path.Combine(tempDir, "b.zip"), "x");

        var orchestrator =
            new AvaloniaGameFileLoadingOrchestrator(new AvaloniaGameCacheService(), new Mock<ILogger>().Object);
        var counts = orchestrator.ComputeSystemCounts([System("Arcade", tempDir), System("Empty", "C:\\missing")]);

        Assert.Equal(2, counts["Arcade"]);
        Assert.Equal(0, counts["Empty"]);
    }

    private static TempDirectory CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sl_av_phase3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new TempDirectory(dir);
    }

    private sealed class TempDirectory : IDisposable
    {
        private readonly string _path;

        public TempDirectory(string path)
        {
            _path = path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_path, true);
            }
            catch
            {
                // best-effort cleanup
            }
        }

        public static implicit operator string(TempDirectory dir)
        {
            return dir._path;
        }
    }
}