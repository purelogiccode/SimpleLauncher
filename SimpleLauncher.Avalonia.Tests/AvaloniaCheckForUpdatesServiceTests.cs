using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Moq;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Core.Interfaces;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="AvaloniaCheckForUpdatesService" /> (Phase 3 service).
///     All HTTP traffic is served by a fake HttpMessageHandler, so no live endpoints are hit.
///     The updater launch flow is exercised against an isolated temp directory — the real
///     updater shipped in the application output is never launched.
/// </summary>
public class AvaloniaCheckForUpdatesServiceTests : IDisposable
{
    private readonly string _updaterDir = Path.Combine(
        Path.GetTempPath(), "SimpleLauncherUpdateTests", Guid.NewGuid().ToString("N"));

    private readonly IServiceProvider? _savedAppServiceProvider;

    public AvaloniaCheckForUpdatesServiceTests()
    {
        // AvaloniaViewSmokeTests installs a fake static App.ServiceProvider that fabricates
        // objects for any type (including UpdateLogWindow). The update flow must not see it —
        // it would divert the launch flow into UI-dispatcher error paths. Save the current
        // value, neutralize it for the duration of the test, and restore it on Dispose.
        _savedAppServiceProvider = GetAppServiceProvider();
        SetAppServiceProvider(null);
    }

    private static string Rid => AvaloniaCheckForUpdatesService.CurrentRuntimeIdentifier;

    public void Dispose()
    {
        SetAppServiceProvider(_savedAppServiceProvider);
        try
        {
            if (Directory.Exists(_updaterDir)) Directory.Delete(_updaterDir, true);
        }
        catch
        {
            // Temp cleanup best-effort
        }
    }

    /// <summary>
    ///     Verifies the Avalonia app consumes the unified release bundle and the single
    ///     Updater.exe asset shared with the WPF app.
    /// </summary>
    [Fact]
    public void AssetNamesMatchUnifiedBundle()
    {
        Assert.Equal($"release_9.9.9_{Rid}.zip",
            AvaloniaCheckForUpdatesService.ReleaseAssetName("9.9.9", Rid));
        Assert.Equal($"updater_{Rid}.zip", AvaloniaCheckForUpdatesService.UpdaterAssetName(Rid));
        Assert.Equal(OperatingSystem.IsWindows() ? "Updater.exe" : "Updater",
            AvaloniaCheckForUpdatesService.UpdaterExecutableName);
    }

    private static string GitHubReleaseJson(string versionTag, params string[] assetNames)
    {
        return JsonSerializer.Serialize(new
        {
            tag_name = versionTag,
            assets = assetNames.Select(n => new
            {
                name = n,
                browser_download_url =
                    $"https://github.com/purelogiccode/SimpleLauncher/releases/download/{versionTag}/{n}"
            })
        });
    }

    private static string LatestReleaseAssetsJson(string versionTag)
    {
        return GitHubReleaseJson(versionTag,
            AvaloniaCheckForUpdatesService.ReleaseAssetName(versionTag.TrimStart('v'), Rid),
            AvaloniaCheckForUpdatesService.UpdaterAssetName(Rid));
    }

    private (AvaloniaCheckForUpdatesService Service, Mock<IMessageBoxLibraryService> MessageBox,
        Mock<IApplicationLifetime> Lifetime)
        CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeMessageHandler(responder);
        var httpClient = new HttpClient(handler);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var messageBox = new Mock<IMessageBoxLibraryService>();
        var logger = new Mock<ILogger>();
        var lifetime = new Mock<IApplicationLifetime>();

        Directory.CreateDirectory(_updaterDir);
        var service = new AvaloniaCheckForUpdatesService(httpClientFactory.Object, messageBox.Object, logger.Object,
            lifetime.Object, _updaterDir);
        return (service, messageBox, lifetime);
    }

    private static HttpResponseMessage Json(string content, int status = 200)
    {
        return new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage Text(string content, int status = 200)
    {
        return new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/plain")
        };
    }

    private static HttpResponseMessage Zip(byte[] bytes, int status = 200)
    {
        return new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new ByteArrayContent(bytes)
        };
    }

    private static byte[] CreateZip(params (string EntryName, byte[] Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var (entryName, content) in entries)
            {
                var entry = archive.CreateEntry(entryName);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        return ms.ToArray();
    }

    [Fact]
    public async Task ManualCheck_NewerVersionOnGitHub_UserAccepts_UpdaterDownloadFails_GuidesToManualDownload()
    {
        var (service, messageBox, lifetime) = CreateService(request =>
        {
            if (request.RequestUri!.Host.StartsWith("api.github.com", StringComparison.Ordinal))
                return Json(LatestReleaseAssetsJson("v9.9.9"));

            // Updater zip download fails
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"), Times.Once);
        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.UpdaterLaunchFailedMessageBoxAsync(), Times.Never);
        lifetime.Verify(m => m.Shutdown(), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_NewerVersionOnGitHub_UserAccepts_UpdaterZipEmpty_GuidesToManualDownload()
    {
        var (service, messageBox, lifetime) = CreateService(request =>
        {
            if (request.RequestUri!.Host.StartsWith("api.github.com", StringComparison.Ordinal))
                return Json(LatestReleaseAssetsJson("v9.9.9"));

            return Zip(CreateZip(("dir/", [])));
        });

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Once);
        lifetime.Verify(m => m.Shutdown(), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_UpdaterMissing_DownloadSucceeds_ExtractsAndAttemptsLaunch()
    {
        var (service, messageBox, lifetime) = CreateService(request =>
        {
            if (request.RequestUri!.Host.StartsWith("api.github.com", StringComparison.Ordinal))
                return Json(LatestReleaseAssetsJson("v9.9.9"));

            // A valid zip carrying a broken "updater" executable — extraction succeeds,
            // the launch attempt then fails (not a valid executable).
            return Zip(CreateZip(
                (AvaloniaCheckForUpdatesService.UpdaterExecutableName, "not an executable"u8.ToArray()),
                ("some-other-file.txt", "x"u8.ToArray())));
        });

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        // The updater was extracted next to the (test) app directory
        Assert.True(File.Exists(Path.Combine(_updaterDir, AvaloniaCheckForUpdatesService.UpdaterExecutableName)));
        Assert.True(File.Exists(Path.Combine(_updaterDir, "some-other-file.txt")));

        messageBox.Verify(m => m.UpdaterLaunchFailedMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Never);
        lifetime.Verify(m => m.Shutdown(), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_UpdaterPresentButBroken_ShowsLaunchFailed()
    {
        var (service, messageBox, lifetime) = CreateService(_ =>
            Json(LatestReleaseAssetsJson("v9.9.9")));

        File.WriteAllText(Path.Combine(_updaterDir, AvaloniaCheckForUpdatesService.UpdaterExecutableName),
            "not an executable");

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.UpdaterLaunchFailedMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Never);
        lifetime.Verify(m => m.Shutdown(), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_UpdaterZipPathTraversal_AbortsExtractionAndShowsManual()
    {
        var (service, messageBox, lifetime) = CreateService(request =>
        {
            if (request.RequestUri!.Host.StartsWith("api.github.com", StringComparison.Ordinal))
                return Json(LatestReleaseAssetsJson("v9.9.9"));

            return Zip(CreateZip(("../evil.txt", "pwned"u8.ToArray())));
        });

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        // The traversal entry must never be written outside the updater directory
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_updaterDir)!, "evil.txt")));
        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Once);
        lifetime.Verify(m => m.Shutdown(), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_UpdaterZipCaseVariantTraversal_AbortsOnUnix()
    {
        if (OperatingSystem.IsWindows()) return; // case-insensitive filesystem: the variant is the same folder

        // LB-17: on a case-sensitive filesystem a case-variant prefix ("/app/" vs "/APP/")
        // must not pass the containment test — OrdinalIgnoreCase used to accept it.
        var destinationName = Path.GetFileName(_updaterDir);
        var caseVariantEntry = $"../{destinationName.ToUpperInvariant()}/evil.txt";

        var (service, messageBox, lifetime) = CreateService(request =>
        {
            if (request.RequestUri!.Host.StartsWith("api.github.com", StringComparison.Ordinal))
                return Json(LatestReleaseAssetsJson("v9.9.9"));

            return Zip(CreateZip((caseVariantEntry, "pwned"u8.ToArray())));
        });

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_updaterDir)!,
            destinationName.ToUpperInvariant(), "evil.txt")));
        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Once);
        lifetime.Verify(m => m.Shutdown(), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_UpdaterAssetMissingFromRelease_ShowsManual()
    {
        var (service, messageBox, lifetime) = CreateService(_ =>
            Json(GitHubReleaseJson("v9.9.9", AvaloniaCheckForUpdatesService.ReleaseAssetName("9.9.9", Rid))));

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Once);
        lifetime.Verify(m => m.Shutdown(), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_NewerVersionOnGitHub_UserDeclines_NoUpdaterFlow()
    {
        var (service, messageBox, lifetime) = CreateService(_ =>
            Json(LatestReleaseAssetsJson("v9.9.9")));

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.No);

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"), Times.Once);
        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Never);
        messageBox.Verify(m => m.UpdaterLaunchFailedMessageBoxAsync(), Times.Never);
        lifetime.Verify(m => m.Shutdown(), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_CurrentVersionUpToDate_ShowsNoUpdateAvailable()
    {
        var (service, messageBox, _) = CreateService(_ =>
            Json(LatestReleaseAssetsJson("v5.6.1")));

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.ThereIsNoUpdateAvailableMessageBoxAsync(It.IsAny<string>()), Times.Once);
        messageBox.Verify(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_GitHubUnavailable_FallsBackToSecondaryServer()
    {
        var (service, messageBox, _) = CreateService(request =>
        {
            var uri = request.RequestUri!;
            if (uri.Host.StartsWith("api.github.com", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            if (uri.Host.StartsWith("assets.purelogiccode.com", StringComparison.Ordinal)) return Text("Version 9.9.9");

            throw new InvalidOperationException("Unexpected request: " + uri);
        });

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"), Times.Once);
        messageBox.Verify(m => m.ErrorCheckingForUpdatesMessageBoxAsync(), Times.Never);
        messageBox.Verify(m => m.InstallUpdateManuallyMessageBoxAsync(), Times.Once); // updater zip is not a valid zip
    }

    [Fact]
    public async Task ManualCheck_GitHubThrows_FallsBackToSecondaryServer()
    {
        var (service, messageBox, _) = CreateService(request =>
        {
            var uri = request.RequestUri!;
            if (uri.Host.StartsWith("api.github.com", StringComparison.Ordinal))
                throw new HttpRequestException("network down");

            if (uri.Host.StartsWith("assets.purelogiccode.com", StringComparison.Ordinal)) return Text("Version 9.9.9");

            throw new InvalidOperationException("Unexpected request: " + uri);
        });

        messageBox.Setup(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"))
            .ReturnsAsync(CoreMessageBoxResult.Yes);

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), "9.9.9.0"), Times.Once);
    }

    [Fact]
    public async Task ManualCheck_AllSourcesUnreachable_ShowsError()
    {
        var (service, messageBox, _) = CreateService(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.ErrorCheckingForUpdatesMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.DoYouWantToUpdateMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ManualCheck_InvalidVersionOnSecondaryServer_ShowsError()
    {
        var (service, messageBox, _) = CreateService(request =>
            request.RequestUri!.Host.StartsWith("api.github.com", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Text("no version information here"));

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.ErrorCheckingForUpdatesMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public async Task ManualCheck_GitHubResponseMissingVersion_ShowsError()
    {
        var (service, messageBox, _) = CreateService(_ =>
            Json("""{"message": "no tag_name here"}"""));

        await service.ManualCheckForUpdatesAsync(null);

        messageBox.Verify(m => m.ErrorCheckingForUpdatesMessageBoxAsync(), Times.Once);
    }

    private static IServiceProvider? GetAppServiceProvider()
    {
        return typeof(App).GetProperty("ServiceProvider", BindingFlags.Static | BindingFlags.Public)?
            .GetValue(null) as IServiceProvider;
    }

    private static void SetAppServiceProvider(IServiceProvider? value)
    {
        var prop = typeof(App).GetProperty("ServiceProvider", BindingFlags.Static | BindingFlags.Public)!;
        prop.GetSetMethod(true)!.Invoke(null, [value]);
    }

    private sealed class FakeMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}