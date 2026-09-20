using System.IO.Compression;
using System.Net;
using System.Reflection;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;
using CheckForUpdatesService = SimpleLauncher.Services.CheckForUpdatesService;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="CheckForUpdatesService" /> covering version comparison, response parsing,
///     ZIP extraction, and real GitHub API interactions.
/// </summary>
public class CheckForUpdatesTests : IDisposable
{
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CheckForUpdatesTests" /> class,
    ///     installing the service provider mock and creating a temporary test directory.
    /// </summary>
    public CheckForUpdatesTests()
    {
        ServiceProviderMock.Install();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_CheckUpdateTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the temporary test directory and restores the service provider mock.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        ServiceProviderMock.Restore();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------
    // IsNewVersionAvailable
    // ------------------------------------------------------------------

    /// <summary>
    ///     Verifies that a newer four-part version (e.g. 5.6.1.0) is detected as an update over the current version.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableFourPartVersionsReturnsTrue()
    {
        var result = InvokeIsNewVersionAvailable("5.6.0.0", "5.6.1.0");
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that two identical four-part versions are not detected as an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableFourPartVersionsEqualReturnsFalse()
    {
        var result = InvokeIsNewVersionAvailable("5.6.0.0", "5.6.0.0");
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that a newer major version is detected as an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableMajorDifferenceReturnsTrue()
    {
        var result = InvokeIsNewVersionAvailable("4.9.9.9", "5.0.0.0");
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that a newer minor version is detected as an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableMinorDifferenceReturnsTrue()
    {
        var result = InvokeIsNewVersionAvailable("5.5.9.0", "5.6.0.0");
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that a newer patch version is detected as an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailablePatchDifferenceReturnsTrue()
    {
        var result = InvokeIsNewVersionAvailable("5.6.0.0", "5.6.0.1");
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that an older current version with a newer major version returns false when compared.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableCurrentNewerMajorReturnsFalse()
    {
        var result = InvokeIsNewVersionAvailable("6.0.0.0", "5.9.9.9");
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that versions with prefixes such as "release" or "v" are compared correctly and a newer version is
    ///     detected.
    /// </summary>
    /// <param name="current">The current version string, possibly prefixed.</param>
    /// <param name="latest">The latest version string, possibly prefixed.</param>
    [Theory]
    [InlineData("release5.3.2", "5.3.3")]
    [InlineData("v5.3.2", "5.3.3")]
    [InlineData("5.3.2", "release5.3.3")]
    public void IsNewVersionAvailablePrefixedVersionsReturnsTrue(string current, string latest)
    {
        var result = InvokeIsNewVersionAvailable(current, latest);
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that identical prefixed versions are not detected as an update.
    /// </summary>
    /// <param name="current">The current version string, possibly prefixed.</param>
    /// <param name="latest">The latest version string, possibly prefixed.</param>
    [Theory]
    [InlineData("release5.3.2", "release5.3.2")]
    [InlineData("v5.3.2", "v5.3.2")]
    public void IsNewVersionAvailableSamePrefixedVersionsReturnsFalse(string current, string latest)
    {
        var result = InvokeIsNewVersionAvailable(current, latest);
        Assert.False(result);
    }

    // ------------------------------------------------------------------
    // NormalizeVersion
    // ------------------------------------------------------------------

    /// <summary>
    ///     Verifies that version strings in various formats are normalized to a four-part version string.
    /// </summary>
    /// <param name="input">The raw version string.</param>
    /// <param name="expected">The expected normalized version string.</param>
    [Theory]
    [InlineData("release5.6.0", "5.6.0.0")]
    [InlineData("v5.6.0", "5.6.0.0")]
    [InlineData("5.6", "5.6.0.0")]
    [InlineData("5", "5.0.0.0")]
    [InlineData("10.20.30.40", "10.20.30.40")]
    [InlineData("release1.2.3.4", "1.2.3.4")]
    [InlineData("v0.1", "0.1.0.0")]
    public void NormalizeVersionVariousFormatsReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, InvokeNormalizeVersion(input));
    }

    /// <summary>
    ///     Verifies that a null version string is normalized to "0.0.0.0".
    /// </summary>
    [Fact]
    public void NormalizeVersionNullReturnsZeroes()
    {
        Assert.Equal("0.0.0.0", InvokeNormalizeVersion(null!));
    }

    /// <summary>
    ///     Verifies that a whitespace-only version string is normalized to "0.0.0.0".
    /// </summary>
    [Fact]
    public void NormalizeVersionWhitespaceOnlyReturnsZeroes()
    {
        Assert.Equal("0.0.0.0", InvokeNormalizeVersion("   "));
    }

    /// <summary>
    ///     Verifies that a version string containing only a prefix is normalized to "0.0.0.0".
    /// </summary>
    [Fact]
    public void NormalizeVersionOnlyPrefixReturnsZeroes()
    {
        Assert.Equal("0.0.0.0", InvokeNormalizeVersion("release"));
    }

    // ------------------------------------------------------------------
    // ParseVersionAndAssetUrlsFromResponse
    // ------------------------------------------------------------------

    /// <summary>
    ///     Verifies that a response with both updater and release assets returns all URLs.
    /// </summary>
    [Fact]
    public void ParseResponseWithBothAssetsReturnsAllUrls()
    {
        const string json = """
                            {
                              "tag_name": "release5.6.0",
                              "assets": [
                                { "name": "updater_win-x64.zip", "browser_download_url": "https://example.com/updater-x64.zip" },
                                { "name": "release_5.6.0_win-x64.zip", "browser_download_url": "https://example.com/release-x64.zip" },
                                { "name": "updater_win-arm64.zip", "browser_download_url": "https://example.com/updater-arm64.zip" },
                                { "name": "release_5.6.0_win-arm64.zip", "browser_download_url": "https://example.com/release-arm64.zip" }
                              ]
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.NotNull(version);
        Assert.NotNull(releaseUrl);
        Assert.NotNull(updaterUrl);
        Assert.StartsWith("5.6.0", version, StringComparison.Ordinal);
        Assert.Contains("updater", updaterUrl, StringComparison.Ordinal);
        Assert.Contains("release", releaseUrl, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that a response with an empty assets array returns only the version.
    /// </summary>
    [Fact]
    public void ParseResponseWithEmptyAssetsArrayReturnsVersionOnly()
    {
        const string json = """
                            {
                              "tag_name": "release5.6.0",
                              "assets": []
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.NotNull(version);
        Assert.Null(releaseUrl);
        Assert.Null(updaterUrl);
    }

    /// <summary>
    ///     Verifies that assets unrelated to the updater or release return null URLs.
    /// </summary>
    [Fact]
    public void ParseResponseWithUnrelatedAssetsReturnsNullsForUrls()
    {
        const string json = """
                            {
                              "tag_name": "release5.6.0",
                              "assets": [
                                { "name": "source.zip", "browser_download_url": "https://example.com/source.zip" },
                                { "name": "readme.txt", "browser_download_url": "https://example.com/readme.txt" }
                              ]
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.NotNull(version);
        Assert.Null(releaseUrl);
        Assert.Null(updaterUrl);
    }

    /// <summary>
    ///     Verifies that an asset missing the browser download URL returns null for that URL.
    /// </summary>
    [Fact]
    public void ParseResponseWithMissingBrowserDownloadUrlReturnsNull()
    {
        const string json = """
                            {
                              "tag_name": "release5.6.0",
                              "assets": [
                                { "name": "updater_win-x64.zip" }
                              ]
                            }
                            """;

        var (version, _, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.NotNull(version);
        Assert.Null(updaterUrl);
    }

    /// <summary>
    ///     Verifies that an empty tag name returns nulls for version and URLs.
    /// </summary>
    [Fact]
    public void ParseResponseWithEmptyTagNameReturnsNulls()
    {
        const string json = """
                            {
                              "tag_name": "",
                              "assets": [
                                { "name": "updater_win-x64.zip", "browser_download_url": "https://example.com/updater.zip" }
                              ]
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.Null(version);
        Assert.Null(releaseUrl);
        Assert.Null(updaterUrl);
    }

    /// <summary>
    ///     Verifies that a non-array assets value returns nulls for version and URLs.
    /// </summary>
    [Fact]
    public void ParseResponseWithAssetsNotArrayReturnsNulls()
    {
        const string json = """
                            {
                              "tag_name": "release5.6.0",
                              "assets": "not an array"
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.Null(version);
        Assert.Null(releaseUrl);
        Assert.Null(updaterUrl);
    }

    /// <summary>
    ///     Verifies that a response with only the updater asset returns null for the release URL.
    /// </summary>
    [Fact]
    public void ParseResponseWithUpdaterOnlyReturnsNullForRelease()
    {
        const string json = """
                            {
                              "tag_name": "release5.6.0",
                              "assets": [
                                { "name": "updater_win-x64.zip", "browser_download_url": "https://example.com/updater.zip" }
                              ]
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.NotNull(version);
        Assert.Null(releaseUrl);
        Assert.NotNull(updaterUrl);
    }

    /// <summary>
    ///     Verifies that a response with only the release asset returns null for the updater URL.
    /// </summary>
    [Fact]
    public void ParseResponseWithReleaseOnlyReturnsNullForUpdater()
    {
        const string json = """
                            {
                              "tag_name": "release5.6.0",
                              "assets": [
                                { "name": "release_5.6.0_win-x64.zip", "browser_download_url": "https://example.com/release.zip" }
                              ]
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.NotNull(version);
        Assert.NotNull(releaseUrl);
        Assert.Null(updaterUrl);
    }

    /// <summary>
    ///     Verifies that a version string containing only a prefix returns nulls for version and URLs.
    /// </summary>
    [Fact]
    public void ParseResponseWithVersionOnlyPrefixReturnsNulls()
    {
        const string json = """
                            {
                              "tag_name": "release",
                              "assets": [
                                { "name": "updater_win-x64.zip", "browser_download_url": "https://example.com/updater.zip" }
                              ]
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.Null(version);
        Assert.Null(releaseUrl);
        Assert.Null(updaterUrl);
    }

    // ------------------------------------------------------------------
    // GetLatestReleaseInfoAsync — fallback chain
    // ------------------------------------------------------------------

    /// <summary>
    ///     Verifies that a 200 response with an unparseable body from the repository
    ///     falls through to the secondary-server fallback instead of returning nulls.
    /// </summary>
    [Fact]
    public async Task GetLatestReleaseInfoUnparseableBodyAndFailingRepositoryFallsBackToSecondaryServer()
    {
        const string unparseableJson = """
                                       { "tag_name": "", "assets": [] }
                                       """;

        var handler = new StubHttpMessageHandler(request =>
        {
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (uri.Contains("repos/purelogiccode/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(unparseableJson)
                };
            }

            if (uri.EndsWith("version.txt", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("release5.7.0") };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var (latestVersion, releasePackageUrl, _, fromFallback) = await InvokeGetLatestReleaseInfoAsync(handler);

        Assert.Equal("5.7.0.0", latestVersion);
        Assert.True(fromFallback);
        Assert.NotNull(releasePackageUrl);
        Assert.Contains("assets.purelogiccode.com", releasePackageUrl, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // ExtractAllFromZip
    // ------------------------------------------------------------------

    /// <summary>
    ///     Verifies that ZIP entries with path traversal sequences are rejected.
    /// </summary>
    [Fact]
    public void ExtractAllFromZipWithPathTraversalReturnsFalse()
    {
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("..\\..\\malicious.txt", CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write("malicious content");
        }

        zipStream.Position = 0;

        var result = CheckForUpdatesService.ExtractAllFromZip(zipStream, _testDirectory, null, new NoOpLogger());

        Assert.False(result, "ExtractAllFromZip should return false for path traversal entries.");
    }

    /// <summary>
    ///     Verifies that ZIP entries with nested directories are extracted to the correct locations.
    /// </summary>
    [Fact]
    public void ExtractAllFromZipWithNestedDirectoriesExtractsCorrectly()
    {
        var zipStream = CreateTestZip(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["level1/level2/level3/deep.txt"] = "deep content",
            ["level1/shallow.txt"] = "shallow content"
        });

        var result = CheckForUpdatesService.ExtractAllFromZip(zipStream, _testDirectory, null, new NoOpLogger());

        Assert.True(result);
        AssertFileContent(Path.Combine(_testDirectory, "level1", "level2", "level3", "deep.txt"), "deep content");
        AssertFileContent(Path.Combine(_testDirectory, "level1", "shallow.txt"), "shallow content");
    }

    /// <summary>
    ///     Verifies that a ZIP with a single file extracts successfully.
    /// </summary>
    [Fact]
    public void ExtractAllFromZipWithSingleFileExtractsSuccessfully()
    {
        var zipStream = CreateTestZip(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["single.txt"] = "only file"
        });

        var result = CheckForUpdatesService.ExtractAllFromZip(zipStream, _testDirectory, null, new NoOpLogger());

        Assert.True(result);
        AssertFileContent(Path.Combine(_testDirectory, "single.txt"), "only file");
    }

    /// <summary>
    ///     Verifies that a ZIP containing large file content extracts without truncation.
    /// </summary>
    [Fact]
    public void ExtractAllFromZipWithLargeContentExtractsSuccessfully()
    {
        var largeContent = new string('X', 100_000);
        var zipStream = CreateTestZip(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["large.bin"] = largeContent
        });

        var result = CheckForUpdatesService.ExtractAllFromZip(zipStream, _testDirectory, null, new NoOpLogger());

        Assert.True(result);
        AssertFileContent(Path.Combine(_testDirectory, "large.bin"), largeContent);
    }

    /// <summary>
    ///     Verifies that existing files are overwritten when extracting a ZIP.
    /// </summary>
    [Fact]
    public void ExtractAllFromZipOverwritesExistingFiles()
    {
        var existingFile = Path.Combine(_testDirectory, "overwrite.txt");
        File.WriteAllText(existingFile, "old content");

        var zipStream = CreateTestZip(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["overwrite.txt"] = "new content"
        });

        var result = CheckForUpdatesService.ExtractAllFromZip(zipStream, _testDirectory, null, new NoOpLogger());

        Assert.True(result);
        AssertFileContent(existingFile, "new content");
    }

    /// <summary>
    ///     Verifies that a corrupted ZIP stream is detected and extraction returns false.
    /// </summary>
    [Fact]
    public void ExtractAllFromZipCorruptedStreamReturnsFalse()
    {
        var corruptedStream = new MemoryStream(("PK\x03\x04c"u8 + "orrupted data"u8).ToArray());

        var result = CheckForUpdatesService.ExtractAllFromZip(corruptedStream, _testDirectory, null, new NoOpLogger());

        Assert.False(result, "ExtractAllFromZip should return false for a corrupted stream.");
    }

    // ------------------------------------------------------------------
    // GetLatestUpdaterInfoAsync — real GitHub API
    // ------------------------------------------------------------------

    /// <summary>
    ///     Verifies that the latest updater info can be retrieved — from the real GitHub API,
    ///     or from the secondary server fallback when GitHub is unreachable/rate-limited.
    /// </summary>
    [Fact]
    public async Task GetLatestUpdaterInfoAsyncReturnsVersionAndUrl()
    {
        var service = CreateCheckerInstance();

        var (updaterUrl, version) = await service.GetLatestUpdaterInfoAsync();

        Assert.NotNull(version);
        Assert.NotNull(updaterUrl);
        Assert.NotEmpty(version);
        Assert.NotEmpty(updaterUrl);
        Assert.StartsWith("5.", version, StringComparison.Ordinal);
        Assert.Contains("updater", updaterUrl, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // DownloadUpdateFileToMemoryAsync — real download from GitHub
    // ------------------------------------------------------------------

    /// <summary>
    ///     Verifies that the updater file can be downloaded from GitHub as a valid ZIP.
    /// </summary>
    [Fact]
    public async Task DownloadUpdateFileToMemoryAsyncFromGitHubDownloadsContent()
    {
        var service = CreateCheckerInstance();

        var (updaterUrl, _) = await service.GetLatestUpdaterInfoAsync();
        Assert.NotNull(updaterUrl);

        await using var memoryStream = new MemoryStream();
        await service.DownloadUpdateFileToMemoryAsync(updaterUrl, memoryStream);

        Assert.True(memoryStream.Length > 0, "Downloaded content should not be empty.");
        memoryStream.Position = 0;

        // Verify it's a valid ZIP (PK header)
        var header = new byte[2];
        _ = await memoryStream.ReadAsync(header);
        Assert.Equal(0x50, header[0]); // 'P'
        Assert.Equal(0x4B, header[1]); // 'K'
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static MemoryStream CreateTestZip(Dictionary<string, string> entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var kvp in entries)
            {
                var entry = archive.CreateEntry(kvp.Key, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(kvp.Value);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void AssertFileContent(string path, string expectedContent)
    {
        Assert.True(File.Exists(path), $"Expected file to exist: {path}");
        var actual = File.ReadAllText(path);
        Assert.Equal(expectedContent, actual);
    }

    private static bool InvokeIsNewVersionAvailable(string? current, string? latest)
    {
        var checker = CreateCheckerInstance();
        var method = typeof(CheckForUpdatesService).GetMethod("IsNewVersionAvailable",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var result = method?.Invoke(checker, [current, latest]);
        return (bool)(result ?? throw new InvalidOperationException("Reflection invoke returned null."));
    }

    private static string InvokeNormalizeVersion(string version)
    {
        var method = typeof(CheckForUpdatesService).GetMethod("NormalizeVersion",
            BindingFlags.NonPublic | BindingFlags.Static);
        var result = method?.Invoke(null, [version]);
        return (string)(result ?? throw new InvalidOperationException("Reflection invoke returned null."));
    }

    private static (string? version, string? releaseUrl, string? updaterUrl) InvokeParseVersionAndAssetUrls(string json)
    {
        var checker = CreateCheckerInstance();
        var method = typeof(CheckForUpdatesService).GetMethod("ParseVersionAndAssetUrlsFromResponse",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var result = method?.Invoke(checker, [json]);
        return ((string?, string?, string?))(result ??
                                             throw new InvalidOperationException("Reflection invoke returned null."));
    }

    private static async Task<(string? latestVersion, string? releasePackageUrl, string? updaterZipAssetUrl,
        bool fromFallback)> InvokeGetLatestReleaseInfoAsync(HttpMessageHandler handler)
    {
        var constructor = typeof(CheckForUpdatesService)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public).First();
        var checker = (CheckForUpdatesService)constructor.Invoke(
            [new StubHttpClientFactory(handler), null, null, new NoOpLogger(), null, null]);
        var method = typeof(CheckForUpdatesService).GetMethod("GetLatestReleaseInfoAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<(string?, string?, string?, bool)>)(method?.Invoke(checker, []) ??
                                                             throw new InvalidOperationException(
                                                                 "Reflection invoke returned null."));
        return await task;
    }

    private static CheckForUpdatesService CreateCheckerInstance()
    {
        var constructor = typeof(CheckForUpdatesService)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public).First();
        var factory = new RealHttpClientFactory();
        var logErrors = new NoOpLogger();
        return (CheckForUpdatesService)constructor.Invoke([factory, null, null, logErrors, null, null]);
    }

    private sealed class RealHttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        ///     Creates a new <see cref="HttpClient" /> instance for the specified name.
        /// </summary>
        /// <param name="name">The logical name of the client.</param>
        /// <returns>A new <see cref="HttpClient" /> instance.</returns>
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    /// <summary>
    ///     An <see cref="IHttpClientFactory" /> that returns clients bound to a stub message handler,
    ///     so tests can script HTTP responses without touching the network.
    /// </summary>
    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler = handler;

        /// <summary>
        ///     Creates a new <see cref="HttpClient" /> bound to the stub handler.
        /// </summary>
        /// <param name="name">The logical name of the client.</param>
        /// <returns>A new <see cref="HttpClient" /> instance.</returns>
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler);
        }
    }

    /// <summary>
    ///     A scripted <see cref="HttpMessageHandler" /> that delegates every request to a responder delegate.
    /// </summary>
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        /// <summary>
        ///     Sends the request to the scripted responder.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="cancellationToken">A cancellation token (unused by the stub).</param>
        /// <returns>The scripted response.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}