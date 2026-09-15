using SimpleLauncher.Core.Services.GetListOfFiles;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="GetListOfFilesService" /> covering file enumeration with various
///     extension filters, recursive/non-recursive modes, and edge cases.
/// </summary>
public class GetListOfFilesServiceTests : IDisposable
{
    private readonly GetListOfFilesService _service;
    private readonly string _testDirectory;


    /// <summary>
    ///     Initializes a new instance of the <see cref="GetListOfFilesServiceTests" /> class, creating a temporary directory
    ///     and service instance.
    /// </summary>
    public GetListOfFilesServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_GetListOfFiles_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _service = new GetListOfFilesService(new NoOpLogger());
    }

    /// <summary>
    ///     Cleans up the temporary test directory.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Verifies that GetFilesAsync returns an empty list for a non-existent directory.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncNonExistentDirectoryReturnsEmptyList()
    {
        var result = await _service.GetFilesAsync(@"C:\nonexistent\dir", ["zip"], false, false);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that GetFilesAsync returns an empty list for an empty directory.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncEmptyDirectoryReturnsEmptyList()
    {
        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], false, false);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that GetFilesAsync finds files with a matching extension.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncFindsFilesWithMatchingExtension()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game1.zip"), "fake");
        File.WriteAllText(Path.Combine(_testDirectory, "game2.zip"), "fake");
        File.WriteAllText(Path.Combine(_testDirectory, "game.nes"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], false, false);

        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.EndsWith(".zip", f, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Verifies that GetFilesAsync finds files with multiple matching extensions.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncFindsFilesWithMultipleExtensions()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.zip"), "fake");
        File.WriteAllText(Path.Combine(_testDirectory, "game.nes"), "fake");
        File.WriteAllText(Path.Combine(_testDirectory, "game.smc"), "fake");
        File.WriteAllText(Path.Combine(_testDirectory, "readme.txt"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, ["zip", "nes", "smc"], false, false);

        Assert.Equal(3, result.Count);
    }

    /// <summary>
    ///     Verifies that the extension filter matches files with uppercase extensions.
    /// </summary>
    [Fact]
    public async Task GetListOfFilesServiceExtensionFilterMatchesUpperCaseExtension()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.ZIP"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], false, false);

        Assert.Single(result);
        Assert.All(result, f => Assert.EndsWith(".ZIP", f, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Verifies that recursive search finds files in subdirectories.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncRecursiveSearchFindsFilesInSubdirectories()
    {
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(_testDirectory, "game1.zip"), "fake");
        File.WriteAllText(Path.Combine(subDir, "game2.zip"), "fake");

        // Recursive: disableRecursiveSearch=false means recursive
        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], false, false);

        Assert.Equal(2, result.Count);
    }

    /// <summary>
    ///     Verifies that non-recursive search does not search subdirectories.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncNonRecursiveSearchDoesNotSearchSubdirectories()
    {
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(_testDirectory, "game1.zip"), "fake");
        File.WriteAllText(Path.Combine(subDir, "game2.zip"), "fake");

        // disableRecursiveSearch=true, groupByFolder=false means non-recursive
        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], true, false);

        Assert.Single(result);
        Assert.Contains("game1.zip", result[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that disableRecursiveSearch means top-level only even when groupByFolder is set
    ///     (the flag must not be overridden; see CORE-28 and IGetListOfFilesService docs).
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncDisableRecursiveSearchWinsOverGroupByFolder()
    {
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(_testDirectory, "game1.zip"), "fake");
        File.WriteAllText(Path.Combine(subDir, "game2.zip"), "fake");

        // disableRecursiveSearch=true means top-level only, regardless of groupByFolder
        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], true, true);

        Assert.Single(result);
        Assert.Contains("game1.zip", result[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that configured extensions with a leading dot still match files (CORE-28).
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncExtensionWithLeadingDotMatches()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.zip"), "fake");
        File.WriteAllText(Path.Combine(_testDirectory, "game.nes"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, [".zip"], false, false);

        Assert.Single(result);
        Assert.EndsWith(".zip", result[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that GetFilesAsync returns empty when no files match the extension filter.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncNoMatchingExtensionsReturnsEmptyList()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.nes"), "fake");
        File.WriteAllText(Path.Combine(_testDirectory, "game.smc"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], false, false);

        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that GetFilesAsync returns empty when the extension list is empty.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncEmptyExtensionListReturnsEmptyList()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.zip"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, [], false, false);

        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that GetFilesAsync throws OperationCanceledException when cancellation is requested.
    /// </summary>
    [Fact]
    public async Task GetListOfFilesServiceCancellationThrowsCancellationException()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.zip"), "fake");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.GetFilesAsync(_testDirectory, ["zip"], false, false, cts.Token));
    }

    /// <summary>
    ///     Verifies that GetFilesAsync returns full file paths.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncReturnsFullPaths()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.zip"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], false, false);

        Assert.Single(result);
        Assert.Equal(Path.Combine(_testDirectory, "game.zip"), result[0]);
    }

    /// <summary>
    ///     Verifies that GetFilesAsync finds files in deeply nested directory structures.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncDeepNestedDirectoryStructure()
    {
        var deepDir = Path.Combine(_testDirectory, "a", "b", "c", "d");
        Directory.CreateDirectory(deepDir);
        File.WriteAllText(Path.Combine(deepDir, "deep.zip"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], false, false);

        Assert.Single(result);
        Assert.Contains("deep.zip", result[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that GetFilesAsync finds files across multiple subdirectories.
    /// </summary>
    [Fact]
    public async Task GetFilesAsyncMultipleFilesInMultipleSubdirectories()
    {
        var sub1 = Path.Combine(_testDirectory, "sub1");
        var sub2 = Path.Combine(_testDirectory, "sub2");
        Directory.CreateDirectory(sub1);
        Directory.CreateDirectory(sub2);

        File.WriteAllText(Path.Combine(_testDirectory, "root.zip"), "fake");
        File.WriteAllText(Path.Combine(sub1, "sub1.zip"), "fake");
        File.WriteAllText(Path.Combine(sub2, "sub2.zip"), "fake");
        File.WriteAllText(Path.Combine(sub2, "sub2.nes"), "fake");

        var result = await _service.GetFilesAsync(_testDirectory, ["zip"], false, false);

        Assert.Equal(3, result.Count);
    }
}