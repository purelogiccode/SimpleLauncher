using System.IO.Compression;
using System.Reflection;
using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;
using CheckForUpdatesService = SimpleLauncher.Services.CheckForUpdatesService;

namespace SimpleLauncher.Tests;

/// <inheritdoc />
/// <summary>
///     Simulates the update extraction logic used by SimpleLauncher.
///     A test ZIP is created in memory, extracted via the real CheckForUpdatesService
///     extraction path, verified, and then every file created during the test
///     is deleted.
/// </summary>
public class UpdateSimulationTests : IDisposable
{
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UpdateSimulationTests" /> class,
    ///     installing the service provider mock and creating a temporary test directory.
    /// </summary>
    public UpdateSimulationTests()
    {
        ServiceProviderMock.Install();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_UpdateTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the temporary test directory and restores the service provider mock.
    /// </summary>
    public void Dispose()
    {
        // Aggressive cleanup: delete everything generated during the test
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

    /// <summary>
    ///     Verifies that a valid ZIP archive is extracted correctly with all files and content matching.
    /// </summary>
    [Fact]
    public void ExtractAllFromZipValidZipExtractsAllFilesSuccessfully()
    {
        // Arrange: build a ZIP in memory that mimics an updater package
        var zipStream = CreateTestZip(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Updater.exe"] = "fake updater binary content",
            ["Updater.dll"] = "fake updater dll content",
            ["subfolder/config.json"] = "{\"version\":\"1.0.0\"}",
            ["README.txt"] = "This is a test update package."
        });

        // Act: use the real extraction logic from CheckForUpdatesService
        var result = CheckForUpdatesService.ExtractAllFromZip(zipStream, _testDirectory, null, new NoOpLogger());

        // Assert: extraction reported success
        Assert.True(result, "ExtractAllFromZip should return true for a valid ZIP.");

        // Assert: files exist on disk with correct content
        AssertFileContent(Path.Combine(_testDirectory, "Updater.exe"), "fake updater binary content");
        AssertFileContent(Path.Combine(_testDirectory, "Updater.dll"), "fake updater dll content");
        AssertFileContent(Path.Combine(_testDirectory, "subfolder", "config.json"), "{\"version\":\"1.0.0\"}");
        AssertFileContent(Path.Combine(_testDirectory, "README.txt"), "This is a test update package.");
    }

    /// <summary>
    ///     Verifies that extracting an empty ZIP archive returns false.
    /// </summary>
    [Fact]
    public void ExtractAllFromZipEmptyZipReturnsFalse()
    {
        // Arrange: create an empty ZIP
        var zipStream = new MemoryStream();
        using (new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            // No entries — archive is created and disposed for side-effect only
        }

        zipStream.Position = 0;

        // Act
        var result = CheckForUpdatesService.ExtractAllFromZip(zipStream, _testDirectory, null, new NoOpLogger());

        // Assert
        Assert.False(result, "ExtractAllFromZip should return false for an empty ZIP.");
    }

    /// <summary>
    ///     Verifies that a newer latest version signals an available update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableLatestGreaterThanCurrentReturnsTrue()
    {
        // Arrange & Act
        var result = InvokeIsNewVersionAvailable("5.3.1", "5.3.2");

        // Assert
        Assert.True(result, "A higher latest version should signal an update is available.");
    }

    /// <summary>
    ///     Verifies that identical versions do not signal an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableSameVersionReturnsFalse()
    {
        var result = InvokeIsNewVersionAvailable("5.3.2", "5.3.2");
        Assert.False(result, "Same version should not signal an update.");
    }

    /// <summary>
    ///     Verifies that a newer current version does not signal an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableCurrentGreaterThanLatestReturnsFalse()
    {
        var result = InvokeIsNewVersionAvailable("5.3.3", "5.3.2");
        Assert.False(result, "Current newer than latest should not signal an update.");
    }

    /// <summary>
    ///     Verifies that various version string formats are normalized to four-part version numbers.
    /// </summary>
    [Fact]
    public void NormalizeVersionVariousInputsNormalizesCorrectly()
    {
        // Act & Assert
        Assert.Equal("5.3.2.0", InvokeNormalizeVersion("release5.3.2"));
        Assert.Equal("1.0.0.0", InvokeNormalizeVersion("v1.0"));
        Assert.Equal("10.20.30.40", InvokeNormalizeVersion("10.20.30.40"));
        Assert.Equal("0.0.0.0", InvokeNormalizeVersion(""));
        Assert.Equal("0.0.0.0", InvokeNormalizeVersion(""));
    }

    /// <summary>
    ///     Verifies that a valid GitHub release JSON response is parsed into the correct version and asset URLs.
    /// </summary>
    [Fact]
    public void ParseVersionAndAssetUrlsFromResponseValidGitHubJsonParsesCorrectly()
    {
        // Arrange
        const string json = """
                            {
                              "tag_name": "release5.3.2",
                              "assets": [
                                { "name": "updater_win-x64.zip", "browser_download_url": "https://example.com/updater.zip" },
                                { "name": "release_5.3.2_win-x64.zip", "browser_download_url": "https://example.com/release.zip" }
                              ]
                            }
                            """;

        // Act
        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        // Assert
        Assert.Equal("5.3.2.0", version);
        Assert.Equal("https://example.com/release.zip", releaseUrl);
        Assert.Equal("https://example.com/updater.zip", updaterUrl);
    }

    /// <summary>
    ///     Verifies that malformed JSON input returns null values for all fields.
    /// </summary>
    [Fact]
    public void ParseVersionAndAssetUrlsFromResponseMalformedJsonReturnsNulls()
    {
        const string json = "this is not valid json {{{";

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        Assert.Null(version);
        Assert.Null(releaseUrl);
        Assert.Null(updaterUrl);
    }

    /// <summary>
    ///     Verifies that JSON missing the tag_name field returns null for the version.
    /// </summary>
    [Fact]
    public void ParseVersionAndAssetUrlsFromResponseMissingTagNameReturnsNulls()
    {
        const string json = """
                            {
                              "assets": [
                                { "name": "updater_win-x64.zip", "browser_download_url": "https://example.com/updater.zip" }
                              ]
                            }
                            """;

        var (version, _, _) = InvokeParseVersionAndAssetUrls(json);

        Assert.Null(version);
    }

    /// <summary>
    ///     Verifies that JSON missing the assets array returns null for all fields.
    /// </summary>
    [Fact]
    public void ParseVersionAndAssetUrlsFromResponseMissingAssetsArrayReturnsNulls()
    {
        const string json = """
                            {
                              "tag_name": "release5.3.2"
                            }
                            """;

        var (version, releaseUrl, updaterUrl) = InvokeParseVersionAndAssetUrls(json);

        // Without assets array, the method returns nulls for all fields
        Assert.Null(version);
        Assert.Null(releaseUrl);
        Assert.Null(updaterUrl);
    }

    /// <summary>
    ///     Verifies that a null current version does not signal an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableNullCurrentReturnsFalse()
    {
        var result = InvokeIsNewVersionAvailable(null, "5.3.2");
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that a null latest version does not signal an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableNullLatestReturnsFalse()
    {
        var result = InvokeIsNewVersionAvailable("5.3.1", null);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that empty version strings do not signal an update.
    /// </summary>
    [Fact]
    public void IsNewVersionAvailableEmptyStringsReturnsFalse()
    {
        var result = InvokeIsNewVersionAvailable("", "");
        Assert.False(result);
    }

    // ------------------------------------------------------------------
    // SilentCheckForUpdatesAsync — timeout handling (bug 68021)
    // ------------------------------------------------------------------

    /// <summary>
    ///     Verifies that an HttpClient timeout during the silent update check stays silent:
    ///     no Error or Warning is logged (Warning+ reaches the bug-report sink) and no
    ///     update UI is shown. Bug 68021 filed a bug report for this expected
    ///     user-environment condition.
    /// </summary>
    [Fact]
    public async Task SilentCheckForUpdatesAsyncHttpTimeoutLogsNoErrorAndShowsNoUi()
    {
        var logger = new Mock<ILogger>();
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var service = CreateTimeoutCheckerInstance(messageBox.Object, logger.Object);

        await service.SilentCheckForUpdatesAsync(null!);

        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        messageBox.Verify(
            m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

    private static (string version, string releaseUrl, string updaterUrl) InvokeParseVersionAndAssetUrls(string json)
    {
        var checker = CreateCheckerInstance();
        var method = typeof(CheckForUpdatesService).GetMethod("ParseVersionAndAssetUrlsFromResponse",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var result = method?.Invoke(checker, [json]);
        return (ValueTuple<string, string, string>)(result ??
                                                    throw new InvalidOperationException(
                                                        "Reflection invoke returned null."));
    }

    private static CheckForUpdatesService CreateCheckerInstance()
    {
        var constructor = typeof(CheckForUpdatesService)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public).First();
        var factory = new MockHttpClientFactory();
        var logErrors = new NoOpLogger();
        return (CheckForUpdatesService)constructor.Invoke([factory, null, null, logErrors, null, null]);
    }

    private static CheckForUpdatesService CreateTimeoutCheckerInstance(
        IMessageBoxLibraryService messageBox, ILogger logger)
    {
        var constructor = typeof(CheckForUpdatesService)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public).First();
        return (CheckForUpdatesService)constructor.Invoke(
            [new TimeoutHttpClientFactory(), messageBox, null, logger, null, null]);
    }

    private sealed class MockHttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        ///     Creates a new <see cref="HttpClient" /> instance, ignoring the requested logical client name.
        /// </summary>
        /// <param name="name">The logical name of the client to create.</param>
        /// <returns>A new <see cref="HttpClient" /> instance.</returns>
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    private sealed class TimeoutHttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        ///     Creates an <see cref="HttpClient" /> that fails every request with a
        ///     <see cref="TaskCanceledException" /> mirroring an <see cref="HttpClient" />.Timeout expiry.
        /// </summary>
        /// <param name="name">The logical name of the client to create.</param>
        /// <returns>An <see cref="HttpClient" /> bound to the timeout stub handler.</returns>
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new TimeoutHttpMessageHandler());
        }
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        /// <summary>
        ///     Fails every request with the same exception shape an
        ///     <see cref="HttpClient" />.Timeout expiry produces (bug 68021).
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="cancellationToken">A cancellation token (unused by the stub).</param>
        /// <returns>A faulted task.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(new TaskCanceledException(
                "The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.",
                new TimeoutException("The operation was canceled.")));
        }
    }
}