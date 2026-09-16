using System.IO.Compression;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.RetroAchievements;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="RetroAchievementsHasherTool" /> covering the archive handling
///     of the per-game hash flow: .zip files are hashed directly (the RetroAchievementsSharp
///     CLI tool pre-loads the first entry itself), while .7z/.rar archives are extracted first.
/// </summary>
public class RetroAchievementsHasherToolTests : IDisposable
{
    private readonly FakeExtractionService _extractionService;
    private readonly FakeFileHasher _fileHasher;
    private readonly RetroAchievementsSystemMatcher _systemMatcher;
    private readonly string _tempFolder;
    private readonly RetroAchievementsHasherTool _tool;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsHasherToolTests" /> class
    ///     with an isolated temporary folder and fake dependencies.
    /// </summary>
    public RetroAchievementsHasherToolTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "SimpleLauncherHasherToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);

        _systemMatcher = new RetroAchievementsSystemMatcher(new NoOpLogger(), new NoOpLogger());
        _extractionService = new FakeExtractionService();
        _fileHasher = new FakeFileHasher();
        _tool = new RetroAchievementsHasherTool(
            new NoOpLogger(),
            _extractionService,
            _ => Task.FromResult<string?>("Nintendo 64"),
            _systemMatcher,
            _fileHasher);
    }

    /// <summary>
    ///     Deletes the temporary folder used by the tests.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Verifies that a .zip is hashed directly (the CLI tool pre-loads the first
    ///     entry itself) and that no extraction happens at all.
    /// </summary>
    [Fact]
    public async Task GetGameHash_ForZipFile_HashesDirectlyWithoutExtraction()
    {
        var zipPath = Path.Combine(_tempFolder, "Game.zip");
        CreateZip(zipPath, "Game.a26", "rom");

        var result = await _tool.GetGameHashForRetroAchievementsAsync(
            zipPath, "Nintendo 64", [".a26"], new NoOpLoadingState(), new NoOpLogger());

        Assert.True(result.IsExtractionSuccessful);
        Assert.Equal("hash-Game", result.Hash);
        Assert.Null(result.TempExtractionPath);
        Assert.Empty(_extractionService.ExtractedArchives);
        Assert.Equal(zipPath, _fileHasher.HashedPaths[0]);
    }

    /// <summary>
    ///     Verifies that a .7z archive is extracted before hashing and that the
    ///     temporary extraction path is returned for cleanup.
    /// </summary>
    [Fact]
    public async Task GetGameHash_ForSevenZipFile_ExtractsBeforeHashing()
    {
        var archivePath = Path.Combine(_tempFolder, "Game.7z");
        File.WriteAllText(archivePath, "archive");

        var result = await _tool.GetGameHashForRetroAchievementsAsync(
            archivePath, "Nintendo 64", [".a26"], new NoOpLoadingState(), new NoOpLogger());

        Assert.True(result.IsExtractionSuccessful);
        Assert.Equal("hash-Game", result.Hash);
        Assert.NotNull(result.TempExtractionPath);
        Assert.Single(_extractionService.ExtractedArchives);
        Assert.Equal(archivePath, _extractionService.ExtractedArchives[0]);
    }

    /// <summary>
    ///     Verifies that a plain (non-archived) ROM is hashed directly.
    /// </summary>
    [Fact]
    public async Task GetGameHash_ForPlainRom_HashesFileDirectly()
    {
        var romPath = Path.Combine(_tempFolder, "Game.a26");
        File.WriteAllText(romPath, "rom");

        var result = await _tool.GetGameHashForRetroAchievementsAsync(
            romPath, "Nintendo 64", [".a26"], new NoOpLoadingState(), new NoOpLogger());

        Assert.True(result.IsExtractionSuccessful);
        Assert.Equal("hash-Game", result.Hash);
        Assert.Null(result.TempExtractionPath);
        Assert.Empty(_extractionService.ExtractedArchives);
    }

    /// <summary>
    ///     Verifies that a .7z archive for an arcade-based system (Naomi, console id 27) is
    ///     hashed by file name without extraction, exactly like Arcade itself.
    /// </summary>
    [Fact]
    public async Task GetGameHash_ForNaomiArchive_HashesByFileNameWithoutExtraction()
    {
        var archivePath = Path.Combine(_tempFolder, "Game.7z");
        File.WriteAllText(archivePath, "archive");

        var result = await _tool.GetGameHashForRetroAchievementsAsync(
            archivePath, "Sega Naomi", [".a26"], new NoOpLoadingState(), new NoOpLogger());

        Assert.Equal("hash-Game", result.Hash);
        Assert.Empty(_extractionService.ExtractedArchives);
        Assert.Equal(archivePath, _fileHasher.HashedPaths[0]);
    }

    /// <summary>
    ///     Verifies the hasher tool delegates hash-support decisions to the system matcher:
    ///     Arcade is supported (even though "SEGA_arcade" is not) and Mega Duck is supported.
    /// </summary>
    /// <param name="systemName">The system name to check.</param>
    /// <param name="expected">Whether the system is expected to be supported.</param>
    [Theory]
    [InlineData("Arcade", true)]
    [InlineData("Mega Duck", true)]
    [InlineData("Sega Naomi", true)]
    [InlineData("Commodore 64", false)]
    [InlineData("PlayStation 3", false)]
    public void IsSystemSupportedForHashingDelegatesToMatcher(string systemName, bool expected)
    {
        Assert.Equal(expected, _tool.IsSystemSupportedForHashing(systemName));
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
    ///     A fake hasher that tracks whether it was called with a file.
    /// </summary>
    private sealed class FakeFileHasher : IRetroAchievementsFileHasher
    {
        /// <summary>
        ///     Gets the file paths that were passed to <see cref="CalculateHashAsync" />.
        /// </summary>
        public List<string> HashedPaths { get; } = [];

        public Task<string?> CalculateHashAsync(string filePath, string systemName,
            CancellationToken cancellationToken = default)
        {
            HashedPaths.Add(filePath);
            return Task.FromResult<string?>($"hash-{Path.GetFileNameWithoutExtension(filePath)}");
        }

        public Task<IReadOnlyDictionary<string, string>> CalculateHashesAsync(
            IReadOnlyCollection<string> filePaths,
            string systemName,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filePath in filePaths)
                results[filePath] = $"hash-{Path.GetFileNameWithoutExtension(filePath)}";

            return Task.FromResult<IReadOnlyDictionary<string, string>>(results);
        }
    }

    /// <summary>
    ///     A fake extraction service that extracts archives into a fake temporary folder.
    /// </summary>
    private sealed class FakeExtractionService : IExtractionService
    {
        /// <summary>
        ///     Gets the archive paths that were extracted.
        /// </summary>
        public List<string> ExtractedArchives { get; } = [];

        public Task<(string? gameFilePath, string? tempDirectoryPath)> ExtractToTempAndGetLaunchFileAsync(
            string archivePath,
            IList<string> fileFormatsToLaunch)
        {
            ExtractedArchives.Add(archivePath);

            var tempDir = Path.Combine(Path.GetTempPath(), "SimpleLauncherHasherToolTests", "FakeTemp");
            Directory.CreateDirectory(tempDir);
            var extension = fileFormatsToLaunch.FirstOrDefault() ?? ".rom";
            var extractedPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(archivePath) + extension);
            File.WriteAllText(extractedPath, "extracted");

            return Task.FromResult<(string?, string?)>((extractedPath, tempDir));
        }

        public Task<bool> ExtractToFolderAsync(string archivePath, string destinationFolder)
        {
            return Task.FromResult(true);
        }
    }

    /// <summary>
    ///     A no-op loading state.
    /// </summary>
    private sealed class NoOpLoadingState : ILoadingState
    {
        public void SetLoadingState(bool isLoading, string? message = null)
        {
        }
    }
}