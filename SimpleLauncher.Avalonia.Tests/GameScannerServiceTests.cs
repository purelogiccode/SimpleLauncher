using System.Reflection;
using Moq;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Avalonia.Services.GameScan;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SanitizeInputString;
using SimpleLauncher.Core.Services.UnifiedSettings;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="GameScannerService" /> — "Microsoft Windows" system creation, shortcut
///     materialization through the registered scanners, idempotence, ignored-name filtering, and the
///     FindMainExecutable/TryGetExeFiles heuristics. All I/O is isolated to a temp system.xml and the
///     test output directory. The database is redirected to an isolated temp file (exclusive
///     collection) so scans never read or write real user data.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class GameScannerServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_GameScanTest_{Guid.NewGuid():N}");

    public GameScannerServiceTests()
    {
        CleanDefaultFolders();
        Directory.CreateDirectory(_tempRoot);
        UnifiedTestDatabase.RedirectToTempDb(_tempRoot);
    }

    private static string DefaultRomsPath =>
        PathHelper.ResolveRelativeToAppDirectory($@"%BASEFOLDER%\roms\{GameScannerService.WindowsSystemName}")!;

    private static string DefaultImagesPath =>
        PathHelper.ResolveRelativeToAppDirectory($@"%BASEFOLDER%\images\{GameScannerService.WindowsSystemName}")!;

    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    private static GameScannerService CreateScanner(string systemXmlPath, params IGamePlatformScanner[] scanners)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(systemXmlPath)!);
        File.WriteAllText(systemXmlPath, "<SystemConfigs />");

        var json = $$"""{"SystemXmlPath": "{{systemXmlPath.Replace("\\", @"\\")}}"}""";
        var config = TestEnvironment.ConfigurationFromJson(json);
        return new GameScannerService(
            TestDependencies.MessageBox().Object,
            config,
            TestDependencies.HttpFactory(new HttpClient()).Object,
            new LoggerConfiguration().CreateLogger(),
            scanners,
            new Mock<IIconExtractor>().Object,
            new SystemManagerService(config));
    }

    /// <summary>
    ///     Removes the default %BASEFOLDER% roms/images folders left by previous test runs
    ///     (same test-bin location every run, so shortcuts would otherwise be skipped).
    /// </summary>
    private static void CleanDefaultFolders()
    {
        try
        {
            if (Directory.Exists(DefaultRomsPath)) Directory.Delete(DefaultRomsPath, true);
            if (Directory.Exists(DefaultImagesPath)) Directory.Delete(DefaultImagesPath, true);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to clean default scanner test folders");
        }
    }

    // ── Orchestration (ScanForStoreGamesCoreAsync) ──

    [Fact]
    public async Task Scan_CreatesSystemEntryAndShortcuts_OnFirstRun()
    {
        var systemXml = Path.Combine(_tempRoot, "system.xml");
        var scanner = CreateScanner(systemXml, new FakeScanner(
        [
            ("Hollow Knight", @"C:\Games\HollowKnight\hollow.exe"),
            ("Doom", @"C:\Games\Doom\doom.exe")
        ]));

        var result = await scanner.ScanForStoreGamesCoreAsync();

        Assert.Equal(2, result.GamesFound);
        Assert.Equal(2, result.ShortcutsCreated);
        Assert.True(result.SystemWasCreated);
        Assert.True(scanner.WasNewSystemCreated);
        Assert.True(Directory.Exists(DefaultImagesPath));

        var hollowShortcut = Path.Combine(DefaultRomsPath, "Hollow Knight.url");
        Assert.True(File.Exists(hollowShortcut));
        var content = File.ReadAllText(hollowShortcut);
        Assert.StartsWith("[InternetShortcut]", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"URL=file:///C:\Games\HollowKnight\hollow.exe", content, StringComparison.OrdinalIgnoreCase);

        var stored = UnifiedSettingsDatabase.LoadSystems();
        var entry = Assert.Single(stored);
        var parsed = SystemConfigStore.Deserialize(entry.Key, entry.Value);
        Assert.NotNull(parsed);
        Assert.Equal(GameScannerService.WindowsSystemName, parsed.SystemName);
        Assert.Equal(["url", "lnk", "bat"], parsed.FileFormatsToSearch, StringComparer.Ordinal);
        Assert.Equal("Direct Launch", Assert.Single(parsed.Emulators).EmulatorName);
        Assert.Equal($@"%BASEFOLDER%\roms\{GameScannerService.WindowsSystemName}",
            Assert.Single(parsed.SystemFolders));
    }

    [Fact]
    public async Task Scan_SecondRun_AddsOnlyNewGames()
    {
        var systemXml = Path.Combine(_tempRoot, "system.xml");
        var scanner = CreateScanner(systemXml, new FakeScanner(
        [
            ("Hollow Knight", @"C:\Games\HollowKnight\hollow.exe"),
            ("Doom", @"C:\Games\Doom\doom.exe")
        ]));

        var first = await scanner.ScanForStoreGamesCoreAsync();
        var second = await scanner.ScanForStoreGamesCoreAsync();

        Assert.Equal(2, first.ShortcutsCreated);
        Assert.Equal(2, first.GamesFound);
        Assert.True(first.SystemWasCreated);

        Assert.Equal(0, second.ShortcutsCreated);
        // Rescanning an already-configured library must still report the games it found
        // (regression: GamesFound used to equal the number of NEW shortcuts).
        Assert.Equal(2, second.GamesFound);
        Assert.False(second.SystemWasCreated);
        Assert.False(scanner.WasNewSystemCreated);

        var stored = UnifiedSettingsDatabase.LoadSystems();
        var entry = Assert.Single(stored);
        Assert.Equal(GameScannerService.WindowsSystemName, entry.Key, StringComparer.OrdinalIgnoreCase);

        // Existing shortcut content is never overwritten
        var doomShortcut = Path.Combine(DefaultRomsPath, "Doom.url");
        var before = File.ReadAllText(doomShortcut);
        await scanner.ScanForStoreGamesCoreAsync();
        Assert.Equal(before, File.ReadAllText(doomShortcut));
    }

    [Fact]
    public async Task Scan_SkipsIgnoredNamesAndEmptyExePaths()
    {
        var systemXml = Path.Combine(_tempRoot, "system.xml");
        var scanner = CreateScanner(systemXml, new FakeScanner(
        [
            ("Spacewar", @"C:\Games\steam\spacewar.exe"), // launcher meta title
            ("Battle.net", @"C:\BattleNet\battle.net.exe"),
            ("NoExe", ""), // no executable resolved
            ("Real Game", @"C:\Games\Real\game.exe")
        ]));

        var result = await scanner.ScanForStoreGamesCoreAsync();

        Assert.Equal(1, result.ShortcutsCreated);
        Assert.False(File.Exists(Path.Combine(DefaultRomsPath, "Spacewar.url")));
        Assert.False(File.Exists(Path.Combine(DefaultRomsPath, "Battle.net.url")));
        Assert.True(File.Exists(Path.Combine(DefaultRomsPath, "Real Game.url")));
    }

    [Fact]
    public async Task Scan_SanitizesGameNamesForFileNames()
    {
        var systemXml = Path.Combine(_tempRoot, "system.xml");
        var scanner = CreateScanner(systemXml, new FakeScanner(
        [
            ("Halo: Combat Evolved (Anniversary)", @"C:\Games\Halo\halo.exe"),
            ("Game..with..dots", @"C:\Games\Dots\game.exe")
        ]));

        var result = await scanner.ScanForStoreGamesCoreAsync();

        Assert.Equal(2, result.ShortcutsCreated);
        var sanitizedHalo = SanitizeInputSystemName.SanitizeFolderName("Halo: Combat Evolved (Anniversary)");
        var sanitizedDots = SanitizeInputSystemName.SanitizeFolderName("Game..with..dots");
        Assert.True(File.Exists(Path.Combine(DefaultRomsPath, $"{sanitizedHalo}.url")));
        Assert.True(File.Exists(Path.Combine(DefaultRomsPath, $"{sanitizedDots}.url")));
    }

    [Fact]
    public async Task Scan_ExistingSystem_UsesItsConfiguredFolder()
    {
        var systemXml = Path.Combine(_tempRoot, "system.xml");
        var customRoms = Path.Combine(_tempRoot, "custom roms");
        Directory.CreateDirectory(customRoms);
        File.WriteAllText(systemXml, $"""
                                      <SystemConfigs>
                                        <SystemConfig>
                                          <SystemName>Microsoft Windows</SystemName>
                                          <SystemFolders>
                                            <SystemFolder>{customRoms}</SystemFolder>
                                          </SystemFolders>
                                          <SystemImageFolder />
                                          <FileFormatsToSearch>
                                            <FormatToSearch>url</FormatToSearch>
                                          </FileFormatsToSearch>
                                          <GroupByFolder>false</GroupByFolder>
                                          <DisableRecursiveSearch>false</DisableRecursiveSearch>
                                        </SystemConfig>
                                      </SystemConfigs>
                                      """);
        var json = $$"""{"SystemXmlPath": "{{systemXml.Replace("\\", @"\\")}}"}""";
        var config = TestEnvironment.ConfigurationFromJson(json);
        UnifiedTestDatabase.SeedSystemsFromXml(config, systemXml);
        var scanner = new GameScannerService(
            TestDependencies.MessageBox().Object,
            config,
            TestDependencies.HttpFactory(new HttpClient()).Object,
            new LoggerConfiguration().CreateLogger(),
            [new FakeScanner([("Skyrim", @"C:\Games\Skyrim\skyrim.exe")])],
            new Mock<IIconExtractor>().Object,
            new SystemManagerService(config));

        var result = await scanner.ScanForStoreGamesCoreAsync();

        Assert.False(result.SystemWasCreated);
        Assert.Equal(1, result.ShortcutsCreated);
        Assert.True(File.Exists(Path.Combine(customRoms, "Skyrim.url")));
        Assert.False(File.Exists(Path.Combine(DefaultRomsPath, "Skyrim.url")));
    }

    [Fact]
    public async Task Scan_RomsFolderNotWritable_ShowsMoveToWritableFolderWarningAndSkipsScan()
    {
        var systemXml = Path.Combine(_tempRoot, "system.xml");
        // A file occupying the configured ROMs path makes folder creation and every
        // shortcut write fail — the "app installed in a protected location" scenario
        // from bugs 66182-66188.
        var blockedRoms = Path.Combine(_tempRoot, "blocked roms");
        File.WriteAllText(blockedRoms, "not a directory");
        File.WriteAllText(systemXml, $"""
                                      <SystemConfigs>
                                        <SystemConfig>
                                          <SystemName>Microsoft Windows</SystemName>
                                          <SystemFolders>
                                            <SystemFolder>{blockedRoms}</SystemFolder>
                                          </SystemFolders>
                                          <SystemImageFolder />
                                          <FileFormatsToSearch>
                                            <FormatToSearch>url</FormatToSearch>
                                          </FileFormatsToSearch>
                                          <GroupByFolder>false</GroupByFolder>
                                          <DisableRecursiveSearch>false</DisableRecursiveSearch>
                                        </SystemConfig>
                                      </SystemConfigs>
                                      """);
        var json = $$"""{"SystemXmlPath": "{{systemXml.Replace("\\", @"\\")}}"}""";
        var config = TestEnvironment.ConfigurationFromJson(json);
        UnifiedTestDatabase.SeedSystemsFromXml(config, systemXml);
        var messageBox = TestDependencies.MessageBox();
        var scanner = new GameScannerService(
            messageBox.Object,
            config,
            TestDependencies.HttpFactory(new HttpClient()).Object,
            new LoggerConfiguration().CreateLogger(),
            [new FakeScanner([("Skyrim", @"C:\Games\Skyrim\skyrim.exe")])],
            new Mock<IIconExtractor>().Object,
            new SystemManagerService(config));

        var result = await scanner.ScanForStoreGamesCoreAsync();

        Assert.Equal(0, result.ShortcutsCreated);
        Assert.False(result.SystemWasCreated);
        Assert.False(File.Exists(Path.Combine(blockedRoms, "Skyrim.url")));
        messageBox.Verify(static m => m.MoveToWritableFolderMessageBoxAsync(), Times.Once);
    }

    // ── IgnoredGameNames ──

    [Fact]
    public void IgnoredGameNamesContainsSteamworksCommonRedistributables()
    {
        Assert.Contains("Steamworks Common Redistributables", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesContainsUnrealEngine()
    {
        Assert.Contains("Unreal Engine", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesContainsDirectX()
    {
        Assert.Contains("DirectX", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesContainsSpacewar()
    {
        Assert.Contains("Spacewar", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesContainsBattleNet()
    {
        Assert.Contains("Battle.net", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesContainsUbisoftConnect()
    {
        Assert.Contains("Ubisoft Connect", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesContainsRockstarGamesLauncher()
    {
        Assert.Contains("Rockstar Games Launcher", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesIsCaseInsensitive()
    {
        Assert.Contains("steamworks common redistributables", GameScannerService.IgnoredGameNames);
        Assert.Contains("STEAMWORKS COMMON REDISTRIBUTABLES", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesDoesNotContainRandomString()
    {
        Assert.DoesNotContain("Some Random Game", GameScannerService.IgnoredGameNames);
    }

    [Fact]
    public void IgnoredGameNamesHasExpectedCount()
    {
        Assert.True(GameScannerService.IgnoredGameNames.Count >= 11,
            $"Expected at least 11 ignored names, got {GameScannerService.IgnoredGameNames.Count}");
    }

    [Fact]
    public void IgnoredGameNames_MatchesWpfScannerSet()
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Steamworks Common Redistributables",
            "Unreal Engine",
            "Fab UE Plugin",
            "Quixel Bridge",
            "DirectX",
            "Google Earth VR",
            "Spacewar",
            "PC Health Check",
            "Rockstar Games Launcher",
            "Battle.net",
            "Ubisoft Connect"
        };

        Assert.Equal(expected, GameScannerService.IgnoredGameNames);
    }

    // ── FindMainExecutable (via reflection since it's private static) ──

    [Fact]
    public void FindMainExecutableReturnsNullForNonExistentDirectory()
    {
        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [Path.Combine(_tempRoot, "nonexistent"), "game", null]);
        Assert.Null(result);
    }

    [Fact]
    public void FindMainExecutableReturnsNullForEmptyDirectory()
    {
        var gameDir = Path.Combine(_tempRoot, "game");
        Directory.CreateDirectory(gameDir);

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "game", null]);
        Assert.Null(result);
    }

    [Fact]
    public void FindMainExecutableReturnsNameMatchExe()
    {
        var gameDir = Path.Combine(_tempRoot, "MyGame");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "MyGame.exe"), "fake exe");
        File.WriteAllText(Path.Combine(gameDir, "other.exe"), "other exe");

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "MyGame", null]) as string;
        Assert.NotNull(result);
        Assert.Equal("MyGame.exe", Path.GetFileName(result));
    }

    [Fact]
    public void FindMainExecutableReturnsContainsMatchExe()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "SuperMyGameLauncher.exe"), "fake exe");
        File.WriteAllText(Path.Combine(gameDir, "other.exe"), "other exe");

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "MyGame", null]) as string;
        Assert.NotNull(result);
        Assert.Contains("MyGame", Path.GetFileName(result), StringComparison.Ordinal);
    }

    [Fact]
    public void FindMainExecutableReturnsSpecificExePathIfProvided()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir2");
        Directory.CreateDirectory(gameDir);
        var specificExe = Path.Combine(gameDir, "specific.exe");
        File.WriteAllText(specificExe, "specific exe");
        File.WriteAllText(Path.Combine(gameDir, "MyGame.exe"), "game exe");

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "MyGame", specificExe]) as string;
        Assert.NotNull(result);
        Assert.Equal("specific.exe", Path.GetFileName(result));
    }

    [Fact]
    public void FindMainExecutableExcludesUninstallerExe()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir3");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "unins000.exe"), "uninstaller");
        File.WriteAllText(Path.Combine(gameDir, "game.exe"), new string('x', 1000));

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "MyGame", null]) as string;
        Assert.NotNull(result);
        Assert.Equal("game.exe", Path.GetFileName(result));
    }

    [Fact]
    public void FindMainExecutableExcludesSetupExe()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir4");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "setup.exe"), "setup");
        File.WriteAllText(Path.Combine(gameDir, "game.exe"), new string('x', 1000));

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "MyGame", null]) as string;
        Assert.NotNull(result);
        Assert.Equal("game.exe", Path.GetFileName(result));
    }

    [Fact]
    public void FindMainExecutableExcludesCrashReporterExe()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir5");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "CrashReporter.exe"), "crash reporter");
        File.WriteAllText(Path.Combine(gameDir, "game.exe"), new string('x', 1000));

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "MyGame", null]) as string;
        Assert.NotNull(result);
        Assert.Equal("game.exe", Path.GetFileName(result));
    }

    [Fact]
    public void FindMainExecutableExcludesLauncherExe()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir6");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "GameLauncherService.exe"), "launcher");
        File.WriteAllText(Path.Combine(gameDir, "game.exe"), new string('x', 1000));

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "MyGame", null]) as string;
        Assert.NotNull(result);
        Assert.Equal("game.exe", Path.GetFileName(result));
    }

    [Fact]
    public void FindMainExecutableLargestExeFallback()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir7");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "small.exe"), "small");
        File.WriteAllText(Path.Combine(gameDir, "large.exe"), new string('x', 5000));

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir, "UnknownGame", null]) as string;
        Assert.NotNull(result);
        Assert.Equal("large.exe", Path.GetFileName(result));
    }

    [Fact]
    public void FindMainExecutableSpecificPathDoesNotExistFallsBackToHeuristics()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir8");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "game.exe"), "game exe");

        var method = typeof(GameScannerService).GetMethod("FindMainExecutable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var nonExistentPath = Path.Combine(gameDir, "nonexistent.exe");
        var result = method.Invoke(null, [gameDir, "game", nonExistentPath]) as string;
        Assert.NotNull(result);
        Assert.Equal("game.exe", Path.GetFileName(result));
    }

    // ── TryGetExeFiles (via reflection since it's private static) ──

    [Fact]
    public void TryGetExeFilesReturnsNullWhenDirectoryVanished()
    {
        var method = typeof(GameScannerService).GetMethod("TryGetExeFiles",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // A directory that no longer exists must yield null, never DirectoryNotFoundException.
        var result = method.Invoke(null, [Path.Combine(_tempRoot, "vanished")]);
        Assert.Null(result);
    }

    [Fact]
    public void TryGetExeFilesReturnsExeFilesForExistingDirectory()
    {
        var gameDir = Path.Combine(_tempRoot, "GameDir9");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "game.exe"), "game exe");
        File.WriteAllText(Path.Combine(gameDir, "readme.txt"), "not an exe");

        var method = typeof(GameScannerService).GetMethod("TryGetExeFiles",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [gameDir]) as string[];
        Assert.NotNull(result);
        var file = Assert.Single(result);
        Assert.Equal("game.exe", Path.GetFileName(file));
    }

    // ── TryEnsureDirectory (via reflection since it's private static) ──

    [Fact]
    public void TryEnsureDirectoryCreatesMissingDirectory()
    {
        var method = typeof(GameScannerService).GetMethod("TryEnsureDirectory",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var dirPath = Path.Combine(_tempRoot, "TryEnsureNew");
        var result = method.Invoke(null, [dirPath, "ROMs", null]);

        Assert.True((bool)result!);
        Assert.True(Directory.Exists(dirPath));
    }

    [Fact]
    public void TryEnsureDirectoryReturnsTrueForExistingDirectory()
    {
        var method = typeof(GameScannerService).GetMethod("TryEnsureDirectory",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var dirPath = Path.Combine(_tempRoot, "TryEnsureExisting");
        Directory.CreateDirectory(dirPath);

        var result = method.Invoke(null, [dirPath, "ROMs", null]);

        Assert.True((bool)result!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryEnsureDirectoryReturnsTrueForNullOrWhitespacePath(string? path)
    {
        var method = typeof(GameScannerService).GetMethod("TryEnsureDirectory",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [path, "ROMs", null]);

        Assert.True((bool)result!);
    }

    [Fact]
    public void TryEnsureDirectoryReturnsFalseWhenCreationFails()
    {
        var method = typeof(GameScannerService).GetMethod("TryEnsureDirectory",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // A file occupying the target path makes Directory.CreateDirectory throw IOException.
        var filePath = Path.Combine(_tempRoot, "TryEnsureOccupiedByFile");
        File.WriteAllText(filePath, "not a directory");

        var silentLogger = new LoggerConfiguration().CreateLogger();
        var result = method.Invoke(null, [filePath, "ROMs", silentLogger]);

        Assert.False((bool)result!);
    }

    // ── TryCopyStoreLogo (via reflection since it's private static) ──

    /// <summary>
    ///     Verifies that an access-denied logo copy from the ACL-protected Microsoft Store package
    ///     folder (C:\Program Files\WindowsApps, simulated here by using a directory as the source,
    ///     which makes File.Copy throw UnauthorizedAccessException) is logged at Information level and
    ///     never at Error, so it is not reported as a bug (bug 66892).
    /// </summary>
    [Fact]
    public async Task TryCopyStoreLogoAccessDeniedLogsInformationNotError()
    {
        var method = typeof(ScanMicrosoftStoreGames).GetMethod("TryCopyStoreLogoAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var sourceDirectory = Path.Combine(_tempRoot, "StoreLogoSource");
        Directory.CreateDirectory(sourceDirectory);
        var destination = Path.Combine(_tempRoot, "logo.png");

        var logger = new Mock<ILogger>();
        var task = (Task<bool>)method.Invoke(null,
            [sourceDirectory, destination, "Forza Horizon 4", logger.Object])!;
        var copied = await task;

        Assert.False(copied);
        logger.Verify(l => l.Information(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    ///     Test scanner that materializes (name, exePath) pairs as .url shortcuts exactly like the
    ///     real storefront scanners do (sanitized name, ignored names filtered).
    /// </summary>
    private sealed class FakeScanner(List<(string Name, string ExePath)> games) : IGamePlatformScanner
    {
        private readonly List<(string Name, string ExePath)> _games = games;

        public async Task ScanAsync(GameScannerService gameScannerService, ILogger logErrors, string windowsRomsPath,
            string windowsImagesPath, ISet<string> ignoredGameNames)
        {
            foreach (var (name, exePath) in _games)
            {
                if (ignoredGameNames.Contains(name)) continue;
                if (string.IsNullOrWhiteSpace(exePath)) continue;

                var sanitized = SanitizeInputSystemName.SanitizeFolderName(name);
                var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitized}.url");
                await File.WriteAllTextAsync(shortcutPath, $"[InternetShortcut]\nURL=file:///{exePath}");
            }
        }
    }
}