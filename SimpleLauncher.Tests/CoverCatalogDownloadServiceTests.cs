using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Services.CoverCatalog;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the R2 missing-cover client using a fake HTTP handler and a temporary
///     image/ROM/cache folder. No real network access is performed.
/// </summary>
public class CoverCatalogDownloadServiceTests : IDisposable
{
    private readonly string _cache;
    private readonly IConfiguration _configuration;
    private readonly string _images;
    private readonly string _roms;
    private readonly SettingsManagerService _settings;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_CoverSvc_{Guid.NewGuid():N}");

    public CoverCatalogDownloadServiceTests()
    {
        _roms = Path.Combine(_tempRoot, "roms");
        _images = Path.Combine(_tempRoot, "images");
        _cache = Path.Combine(_tempRoot, "cache");
        Directory.CreateDirectory(_roms);
        Directory.CreateDirectory(_images);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ApiSettings:CoverCatalogUrl"] = "https://covers.test/v1",
                ["ImageExtensions:0"] = ".png",
                ["ImageExtensions:1"] = ".jpg",
                ["ImageExtensions:2"] = ".jpeg"
            })
            .Build();

        _settings = new SettingsManagerService(_configuration, new NoOpLogger(), new NoOpCredentialProtector())
        {
            EnableAnnotationStripping = true,
            EnableFuzzyMatching = true,
            FuzzyMatchingThreshold = 0.8
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // best effort
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DownloadMissingCovers_DownloadsOnlyMissingAndSkipsExisting()
    {
        WriteRom("Super Mario Bros. (USA).nes");
        WriteRom("Already Covered.nes");
        WriteRom("Unknown Game.nes");
        File.WriteAllBytes(Path.Combine(_images, "Already Covered.jpg"), [9, 9, 9]);

        var handler = CreateHandler();
        var service = CreateService(handler);

        var result = await service.DownloadMissingCoversAsync(
            CreateRequest("Super Mario Bros. (USA).nes", "Already Covered.nes", "Unknown Game.nes"));

        Assert.Equal(CoverDownloadStatus.Completed, result.Status);
        Assert.Equal(1, result.Downloaded);
        Assert.Equal(1, result.AlreadyPresent);
        Assert.Equal(1, result.NotFound);
        Assert.True(File.Exists(Path.Combine(_images, "Super Mario Bros. (USA).jpg")));
        Assert.False(File.Exists(Path.Combine(_images, "Unknown Game.jpg")));
        Assert.Equal(1, handler.ImageRequests);
    }

    [Fact]
    public async Task DownloadMissingCovers_FuzzyMatchesVariant()
    {
        WriteRom("Super Mario Brothers (USA).nes");
        var handler = CreateHandler();
        var service = CreateService(handler);

        var result = await service.DownloadMissingCoversAsync(
            CreateRequest("Super Mario Brothers (USA).nes"));

        Assert.Equal(1, result.Downloaded);
        Assert.True(File.Exists(Path.Combine(_images, "Super Mario Brothers (USA).jpg")));
    }

    [Fact]
    public async Task DownloadMissingCovers_404CountsAsNotFound()
    {
        WriteRom("Gone.nes");
        var handler = CreateHandler(notFoundImage: true);
        var service = CreateService(handler);

        var result = await service.DownloadMissingCoversAsync(CreateRequest("Gone.nes"));

        Assert.Equal(0, result.Downloaded);
        Assert.Equal(1, result.NotFound);
        Assert.False(File.Exists(Path.Combine(_images, "Gone.jpg")));
    }

    [Fact]
    public async Task DownloadMissingCovers_SystemNotInCatalog()
    {
        WriteRom("Super Mario Bros. (USA).nes");
        var service = CreateService(CreateHandler());

        var result = await service.DownloadMissingCoversAsync(new CoverDownloadRequest
        {
            SystemName = "Unknown System",
            SystemImageFolder = _images,
            GameFilePaths = [Path.Combine(_roms, "Super Mario Bros. (USA).nes")]
        });

        Assert.Equal(CoverDownloadStatus.SystemNotAvailable, result.Status);
    }

    [Fact]
    public async Task DownloadMissingCovers_UsesFreshCacheOnSecondRun()
    {
        WriteRom("Super Mario Bros. (USA).nes");
        var handler = CreateHandler();
        var service = CreateService(handler);

        var first = await service.DownloadMissingCoversAsync(CreateRequest("Super Mario Bros. (USA).nes"));
        var requestsAfterFirst = handler.TotalRequests;
        var second = await service.DownloadMissingCoversAsync(CreateRequest("Super Mario Bros. (USA).nes"));

        Assert.Equal(CoverDownloadStatus.NoMissingCovers, second.Status);
        Assert.Equal(1, first.Downloaded);
        Assert.Equal(1, second.AlreadyPresent);
        Assert.Equal(requestsAfterFirst, handler.TotalRequests);
    }

    [Fact]
    public async Task DownloadMissingCovers_NoGamesReturnsNoGames()
    {
        var service = CreateService(CreateHandler());
        var result = await service.DownloadMissingCoversAsync(new CoverDownloadRequest
        {
            SystemName = "Nintendo NES",
            SystemImageFolder = _images,
            GameFilePaths = []
        });

        Assert.Equal(CoverDownloadStatus.NoGames, result.Status);
    }

    private CoverCatalogDownloadService CreateService(TestHandler handler)
    {
        return new CoverCatalogDownloadService(_configuration, new FakeHttpClientFactory(handler),
            new NoOpLogger(), _settings, _cache);
    }

    private CoverDownloadRequest CreateRequest(params string[] romNames)
    {
        return new CoverDownloadRequest
        {
            SystemName = "Nintendo NES",
            SystemImageFolder = _images,
            GameFilePaths = romNames.Select(name => Path.Combine(_roms, name)).ToList()
        };
    }

    private void WriteRom(string name)
    {
        File.WriteAllText(Path.Combine(_roms, name), "rom");
    }

    private static TestHandler CreateHandler(bool notFoundImage = false)
    {
        const string catalog = """
                               {
                                 "version": 1,
                                 "baseUrl": "https://covers.test/v1",
                                 "systems": [
                                   { "name": "Nintendo NES", "slug": "nintendo-nes", "count": 3, "indexFile": "nintendo-nes/index.json" }
                                 ]
                               }
                               """;
        const string index = """
                             {
                               "version": 1,
                               "system": "Nintendo NES",
                               "slug": "nintendo-nes",
                               "images": [
                                 {
                                   "file": "Super Mario Bros. (USA).jpg",
                                   "names": ["Super Mario Bros. (USA)", "Super Mario Bros.", "Super Mario Brothers (USA)"]
                                 },
                                 {
                                   "file": "Gone.jpg",
                                   "names": ["Gone"]
                                 }
                               ]
                             }
                             """;

        return new TestHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/catalog.json", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, catalog);
            if (path.EndsWith("/nintendo-nes/index.json", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, index);
            if (path.EndsWith(".jpg", StringComparison.Ordinal))
            {
                if (notFoundImage) return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int ImageRequests { get; private set; }
        public int TotalRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            TotalRequests++;
            if (request.RequestUri?.AbsolutePath.EndsWith(".jpg", StringComparison.Ordinal) == true)
                ImageRequests++;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }
}
