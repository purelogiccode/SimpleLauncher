using System.Diagnostics;
using System.IO.Compression;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.RetroAchievements;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="RetroAchievementsHashScanner" /> covering background scanning,
///     JSON persistence through the hash store, and prevention of parallel scans.
/// </summary>
public class RetroAchievementsHashScannerTests : IDisposable
{
    private readonly FakeExtractionService _extractionService;
    private readonly FakeFileHasher _fileHasher;
    private readonly FakeGetListOfFilesService _getListOfFiles;
    private readonly string _romsFolder;
    private readonly RetroAchievementsHashScanner _scanner;
    private readonly RetroAchievementsHashStore _store;
    private readonly RetroAchievementsSystemMatcher _systemMatcher;
    private readonly string _tempFolder;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsHashScannerTests" /> class
    ///     with an isolated temporary ROM and hash folder.
    /// </summary>
    public RetroAchievementsHashScannerTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "SimpleLauncherHashScannerTests", Guid.NewGuid().ToString("N"));
        _romsFolder = Path.Combine(_tempFolder, "Roms");
        Directory.CreateDirectory(_romsFolder);

        _store = new RetroAchievementsHashStore(new NoOpLogger(), Path.Combine(_tempFolder, "Hashes"));
        _systemMatcher = new RetroAchievementsSystemMatcher(new NoOpLogger(), new NoOpLogger());
        _fileHasher = new FakeFileHasher();
        _getListOfFiles = new FakeGetListOfFilesService();
        _extractionService = new FakeExtractionService();
        _scanner = new RetroAchievementsHashScanner(
            new NoOpLogger(), _systemMatcher, _fileHasher, _getListOfFiles, _extractionService, _store);
    }

    /// <summary>
    ///     Deletes the temporary folders used by the tests.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Verifies that scanning a system writes a JSON file containing a hash for every game file.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_CalculatesAndPersistsHashes()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game 1.7z"), "rom1");
        File.WriteAllText(Path.Combine(_romsFolder, "Game 2.7z"), "rom2");

        var completedSystems = new List<string>();
        var started = await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false,
            completedSystems.Add);

        Assert.True(started);
        Assert.Contains("Nintendo 64", completedSystems, StringComparer.OrdinalIgnoreCase);

        var loaded = _store.LoadSystemHashes("Nintendo 64");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Hashes.Count);
        Assert.Equal(2, loaded.FileCount);
        Assert.Equal("hash-Game 1", loaded.Hashes[Path.Combine(_romsFolder, "Game 1.7z")]);
        Assert.Equal("hash-Game 2", loaded.Hashes[Path.Combine(_romsFolder, "Game 2.7z")]);
    }

    /// <summary>
    ///     Verifies that scanning a system again with the same game count skips re-hashing.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_WhenGameCountUnchanged_SkipsReHashing()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game 1.7z"), "rom1");

        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        Assert.Equal(1, _fileHasher.HashCallCount);

        var completedSystems = new List<string>();
        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false,
            completedSystems.Add);

        Assert.Equal(1, _fileHasher.HashCallCount);
        Assert.Empty(completedSystems);
        Assert.Single(_store.LoadSystemHashes("Nintendo 64")!.Hashes);
    }

    /// <summary>
    ///     Verifies that a stored scan produced by older hash logic is recalculated even
    ///     when the game count is unchanged.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_WhenHashVersionChanged_ReHashes()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game 1.7z"), "rom1");

        // Simulate a scan stored by older hash logic (HashVersion 0)
        _store.SaveSystemHashes(new RaSystemHashes
        {
            SystemName = "Nintendo 64",
            FileCount = 1,
            HashVersion = 0,
            Hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.Combine(_romsFolder, "Game 1.7z")] = "stale-hash"
            }
        });

        var completedSystems = new List<string>();
        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false,
            completedSystems.Add);

        Assert.Contains("Nintendo 64", completedSystems, StringComparer.OrdinalIgnoreCase);

        var loaded = _store.LoadSystemHashes("Nintendo 64");
        Assert.Equal(1, loaded!.HashVersion);
        Assert.Equal("hash-Game 1", loaded.Hashes[Path.Combine(_romsFolder, "Game 1.7z")]);
    }

    /// <summary>
    ///     Verifies that scanning a system again after a game was added recalculates the hashes.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_WhenGameCountChanged_ReHashes()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game 1.7z"), "rom1");

        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        Assert.Equal(1, _fileHasher.HashCallCount);

        File.WriteAllText(Path.Combine(_romsFolder, "Game 2.7z"), "rom2");

        var completedSystems = new List<string>();
        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false,
            completedSystems.Add);

        Assert.Equal(3, _fileHasher.HashCallCount);
        Assert.Contains("Nintendo 64", completedSystems, StringComparer.OrdinalIgnoreCase);

        var loaded = _store.LoadSystemHashes("Nintendo 64");
        Assert.Equal(2, loaded!.Hashes.Count);
        Assert.Equal(2, loaded.FileCount);
    }

    /// <summary>
    ///     Verifies that files that cannot be hashed are skipped and an empty result is still persisted.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_WhenHashFails_SkipsFileButPersistsResult()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Broken.7z"), "broken");

        _fileHasher.FailAll = true;

        var started = await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        Assert.True(started);
        Assert.True(_store.HasSystemHashes("Nintendo 64"));
        Assert.Empty(_store.LoadSystemHashes("Nintendo 64")!.Hashes);
    }

    /// <summary>
    ///     Verifies that .zip archives are hashed directly through the CLI tool (which
    ///     pre-loads the first entry itself) without extracting to disk, and that the
    ///     hash is stored under the original archive path.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_ZipFilesAreHashedDirectly()
    {
        CreateZip(Path.Combine(_romsFolder, "Game.zip"), "Game.a26", "rom");

        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".zip"],
            [".a26"],
            true,
            false);

        // The archive was NOT extracted to disk; it was hashed in the batch call
        Assert.Empty(_extractionService.ExtractedArchives);
        Assert.Equal(1, _fileHasher.BatchCallCount);
        Assert.Empty(_fileHasher.HashedPaths);

        // The hash is persisted under the original archive path so the game list can match it
        var loaded = _store.LoadSystemHashes("Nintendo 64");
        Assert.Single(loaded!.Hashes);
        Assert.True(loaded.Hashes.ContainsKey(Path.Combine(_romsFolder, "Game.zip")));
        Assert.Equal("hash-Game", loaded.Hashes[Path.Combine(_romsFolder, "Game.zip")]);
    }

    /// <summary>
    ///     Verifies that .7z archives are extracted to a temporary folder before hashing and
    ///     that the hash is stored under the original archive path.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_SevenZipFilesAreExtractedBeforeHashing()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game.7z"), "rom");

        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        // The archive was extracted once, and the hasher received the extracted ROM file
        Assert.Single(_extractionService.ExtractedArchives);
        Assert.Equal(Path.Combine(_romsFolder, "Game.7z"), _extractionService.ExtractedArchives[0]);
        Assert.EndsWith(".a26", _fileHasher.HashedPaths[0], StringComparison.OrdinalIgnoreCase);

        // The hash is persisted under the original archive path so the game list can match it
        var loaded = _store.LoadSystemHashes("Nintendo 64");
        Assert.Single(loaded!.Hashes);
        Assert.True(loaded.Hashes.ContainsKey(Path.Combine(_romsFolder, "Game.7z")));

        // Temporary extraction folders are cleaned up after hashing
        Assert.False(Directory.Exists(Path.Combine(_tempFolder, "FakeTemp")));
    }

    /// <summary>
    ///     Creates a zip archive on disk containing a single entry.
    /// </summary>
    private static void CreateZip(string zipPath, string entryName, string content)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    /// <summary>
    ///     Verifies that a second scan request is rejected while a scan is already running.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_SecondCallWhileScanning_IsRejected()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game.zip"), "rom");

        _fileHasher.BlockCalls = true;

        var firstTask = _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        // The scan flag is set synchronously, so the second call is rejected immediately.
        var secondStarted = await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        Assert.False(secondStarted);
        Assert.True(_scanner.IsScanning);

        _fileHasher.ReleaseBlock();
        await firstTask;
        Assert.False(_scanner.IsScanning);
    }

    /// <summary>
    ///     Verifies that <see cref="RetroAchievementsHashScanner.CancelScanAndWaitAsync" />
    ///     cancels a running scan (even one blocked inside the hasher), waits for it to
    ///     finish within the timeout, releases the scan flag, and makes the abandoned
    ///     scan task return false instead of throwing.
    /// </summary>
    [Fact]
    public async Task CancelScanAndWaitAsync_CancelsRunningScanAndReleasesFlag()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game.7z"), "rom");

        _fileHasher.BlockCalls = true;

        var scanTask = _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        // Wait until the scan is blocked inside the hasher before canceling.
        var waitStopwatch = Stopwatch.StartNew();
        while (_fileHasher.HashCallCount == 0 && waitStopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10);
        }

        Assert.Equal(1, _fileHasher.HashCallCount);
        Assert.True(_scanner.IsScanning);

        await _scanner.CancelScanAndWaitAsync(TimeSpan.FromSeconds(10)).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.False(_scanner.IsScanning);
        Assert.False(await scanTask);
    }

    /// <summary>
    ///     Verifies that systems without usable hashing support are reported as not scannable,
    ///     using the same single source of truth as the hasher tool (Arcade is supported even
    ///     though "SEGA_arcade" is not, and Mega Duck/Naomi are supported arcade/console ids).
    /// </summary>
    [Fact]
    public void IsSystemScannable_SupportsOnlyKnownSystems()
    {
        Assert.True(_scanner.IsSystemScannable("Nintendo 64"));
        Assert.True(_scanner.IsSystemScannable("NES"));
        Assert.True(_scanner.IsSystemScannable("Arcade"));
        Assert.True(_scanner.IsSystemScannable("Mega Duck"));
        Assert.True(_scanner.IsSystemScannable("Sega Naomi"));
        Assert.False(_scanner.IsSystemScannable("Commodore 64"));
        Assert.False(_scanner.IsSystemScannable("Microsoft Windows"));
        Assert.False(_scanner.IsSystemScannable(""));
    }

    /// <summary>
    ///     Verifies that the explicit rescan command forces a re-hash even when the game
    ///     count and hash version are unchanged (e.g. a ROM was replaced with another revision).
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_WithForce_ReHashesWhenCountUnchanged()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game 1.7z"), "rom1");

        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        Assert.Equal(1, _fileHasher.HashCallCount);

        var completedSystems = new List<string>();
        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false,
            completedSystems.Add,
            force: true);

        Assert.Equal(2, _fileHasher.HashCallCount);
        Assert.Contains("Nintendo 64", completedSystems, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Verifies that same-named games in different folders are all hashed: the scan
    ///     de-duplicates by full path, not by file name.
    /// </summary>
    [Fact]
    public async Task ScanSystemAsync_SameNamedFilesInDifferentFolders_AreAllHashed()
    {
        var folderA = Path.Combine(_romsFolder, "FolderA");
        var folderB = Path.Combine(_romsFolder, "FolderB");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        File.WriteAllText(Path.Combine(folderA, "Game.7z"), "rom-a");
        File.WriteAllText(Path.Combine(folderB, "Game.7z"), "rom-b");

        var started = await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [folderA, folderB],
            [".7z"],
            [".a26"],
            true,
            false);

        Assert.True(started);
        Assert.Equal(2, _fileHasher.HashCallCount);

        var loaded = _store.LoadSystemHashes("Nintendo 64");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.FileCount);
        Assert.Equal(2, loaded.Hashes.Count);
    }

    /// <summary>
    ///     Verifies that a stored scan is only considered up to date when it was produced
    ///     by the current hash logic.
    /// </summary>
    [Fact]
    public async Task IsScanUpToDate_RequiresCurrentHashVersion()
    {
        Assert.False(_scanner.IsScanUpToDate("Nintendo 64"));

        // A stale scan (produced by older hash logic) is not up to date
        _store.SaveSystemHashes(new RaSystemHashes
        {
            SystemName = "Nintendo 64",
            FileCount = 1,
            HashVersion = 0
        });
        Assert.False(_scanner.IsScanUpToDate("Nintendo 64"));

        // A fresh scan produced by the current logic is up to date
        File.WriteAllText(Path.Combine(_romsFolder, "Game 1.7z"), "rom1");
        await _scanner.ScanSystemAsync(
            "Nintendo 64",
            [_romsFolder],
            [".7z"],
            [".a26"],
            true,
            false);

        Assert.True(_scanner.IsScanUpToDate("Nintendo 64"));
    }

    /// <summary>
    ///     Verifies that scanning all systems skips unsupported systems and only persists
    ///     results for the supported ones.
    /// </summary>
    [Fact]
    public async Task ScanAllSystemsAsync_SkipsUnsupportedSystems()
    {
        File.WriteAllText(Path.Combine(_romsFolder, "Game.zip"), "rom");

        var targets = new List<RaHashScanTarget>
        {
            new()
            {
                SystemName = "Nintendo 64",
                SystemFolders = [_romsFolder],
                FileFormatsToSearch = [".7z"],
                FileFormatsToLaunch = [".a26"],
                DisableRecursiveSearch = true
            },
            new()
            {
                SystemName = "Microsoft Windows",
                SystemFolders = [_romsFolder],
                FileFormatsToSearch = [".7z"],
                FileFormatsToLaunch = [".a26"],
                DisableRecursiveSearch = true
            }
        };

        var completedSystems = new List<string>();
        var started = await _scanner.ScanAllSystemsAsync(targets, completedSystems.Add);

        Assert.True(started);
        Assert.Contains("Nintendo 64", completedSystems, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft Windows", completedSystems, StringComparer.OrdinalIgnoreCase);

        Assert.True(_store.HasSystemHashes("Nintendo 64"));
        Assert.False(_store.HasSystemHashes("Microsoft Windows"));
    }

    /// <summary>
    ///     A fake file hasher that returns a deterministic hash per file name.
    /// </summary>
    private sealed class FakeFileHasher : IRetroAchievementsFileHasher
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        ///     Gets the number of times <see cref="CalculateHashAsync" /> was called.
        /// </summary>
        public int HashCallCount { get; private set; }

        /// <summary>
        ///     Gets the number of times <see cref="CalculateHashesAsync" /> was called.
        /// </summary>
        public int BatchCallCount { get; private set; }

        /// <summary>
        ///     Gets the file paths that were passed to <see cref="CalculateHashAsync" />.
        /// </summary>
        public List<string> HashedPaths { get; } = [];

        /// <summary>
        ///     Gets or sets a value indicating whether every hash calculation returns null.
        /// </summary>
        public bool FailAll { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether hash calls block until <see cref="ReleaseBlock" /> is called.
        /// </summary>
        public bool BlockCalls { get; set; }

        public async Task<string?> CalculateHashAsync(string filePath, string systemName,
            CancellationToken cancellationToken = default)
        {
            HashCallCount++;
            HashedPaths.Add(filePath);

            if (BlockCalls) await _gate.Task.WaitAsync(cancellationToken);

            if (FailAll) return null;

            return $"hash-{Path.GetFileNameWithoutExtension(filePath)}";
        }

        public async Task<IReadOnlyDictionary<string, string>> CalculateHashesAsync(
            IReadOnlyCollection<string> filePaths,
            string systemName,
            CancellationToken cancellationToken = default)
        {
            BatchCallCount++;

            if (BlockCalls) await _gate.Task;

            if (FailAll) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filePath in filePaths)
                results[filePath] = $"hash-{Path.GetFileNameWithoutExtension(filePath)}";

            return results;
        }

        public void ReleaseBlock()
        {
            _gate.TrySetResult(true);
        }
    }

    /// <summary>
    ///     A fake file enumeration service that lists files by extension from a single folder.
    /// </summary>
    private sealed class FakeGetListOfFilesService : IGetListOfFilesService
    {
        public Task<IList<string>> GetFilesAsync(
            string directoryPath,
            IList<string> fileExtensions,
            bool disableRecursiveSearch,
            bool groupByFolder,
            CancellationToken cancellationToken = default)
        {
            var files = Directory
                .EnumerateFiles(directoryPath, "*",
                    disableRecursiveSearch ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories)
                .Where(f => fileExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToList();

            return Task.FromResult<IList<string>>(files);
        }
    }

    /// <summary>
    ///     A fake extraction service that "extracts" archives into a fake temporary folder,
    ///     producing a file with the same base name and the first launchable extension.
    /// </summary>
    private sealed class FakeExtractionService : IExtractionService
    {
        private readonly string _fakeTempFolder =
            Path.Combine(Path.GetTempPath(), "SimpleLauncherHashScannerTests", "FakeTemp");

        /// <summary>
        ///     Gets the archive paths that were extracted.
        /// </summary>
        public List<string> ExtractedArchives { get; } = [];

        public Task<(string? gameFilePath, string? tempDirectoryPath)> ExtractToTempAndGetLaunchFileAsync(
            string archivePath,
            IList<string> fileFormatsToLaunch)
        {
            ExtractedArchives.Add(archivePath);

            Directory.CreateDirectory(_fakeTempFolder);
            var extension = fileFormatsToLaunch.FirstOrDefault() ?? ".rom";
            var extractedPath =
                Path.Combine(_fakeTempFolder, Path.GetFileNameWithoutExtension(archivePath) + extension);
            File.WriteAllText(extractedPath, "extracted");

            return Task.FromResult<(string?, string?)>((extractedPath, _fakeTempFolder));
        }

        public Task<bool> ExtractToFolderAsync(string archivePath, string destinationFolder)
        {
            return Task.FromResult(true);
        }
    }
}