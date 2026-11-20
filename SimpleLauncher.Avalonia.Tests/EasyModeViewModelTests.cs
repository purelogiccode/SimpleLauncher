using System.Net;
using System.Text;
using Moq;
using SimpleLauncher.Avalonia.Services.AvaloniaServices;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.DownloadService;
using SimpleLauncher.Core.Services.EasyMode;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the EasyMode "Add System" ViewModel (WPF EasyModeWindow parity).
///     The EasyMode configuration is served by a fake HTTP handler, image-pack button
///     state is derived from the selected system, and the download/extraction flow is
///     exercised end-to-end through a real DownloadManager (extraction itself is mocked
///     and gated so the loading overlay state can be asserted mid-extraction).
/// </summary>
public class EasyModeViewModelTests
{
    private const string EasyModeJson = """
                                        [
                                          {
                                            "systemName": "NES",
                                            "systemFolder": "roms/nes",
                                            "systemImageFolder": "images/nes",
                                            "fileFormatsToSearch": [".nes"],
                                            "fileFormatsToLaunch": [".nes"],
                                            "emulators": {
                                              "emulator": {
                                                "emulatorName": "Mesen",
                                                "emulatorDownloadLink": "https://example.com/emulator.zip",
                                                "emulatorDownloadExtractPath": "emulators/nes",
                                                "imagePackDownloadLink": "https://example.com/pack1.zip",
                                                "imagePackDownloadLink2": "https://example.com/pack2.zip",
                                                "imagePackDownloadExtractPath": "images/nes"
                                              }
                                            }
                                          },
                                          {
                                            "systemName": "NoPackSystem",
                                            "systemFolder": "roms/nopack",
                                            "systemImageFolder": "images/nopack",
                                            "fileFormatsToSearch": [".xyz"],
                                            "fileFormatsToLaunch": [".xyz"],
                                            "emulators": { "emulator": { "emulatorName": "NoPacker" } }
                                          }
                                        ]
                                        """;

    internal static EasyModeViewModel CreateVm(Mock<IExtractionService>? extraction = null)
    {
        HeadlessAvalonia.EnsureInitialized();

        var messageBox = TestDependencies.MessageBox();
        var logger = TestDependencies.Logger();

        var settings = TestDependencies.Settings(messageBox: messageBox);
        settings.EnableNotificationSound = false;
        var playSound = TestDependencies.PlaySound(settings);

        // Force the API path: no local easymode.xml in the test output.
        File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "easymode.xml"));
        File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "easymode_arm64.xml"));

        var config = TestEnvironment.ConfigurationFromJson("""{"EasyModeCacheDurationMinutes": "0"}""");
        var httpClient = new HttpClient(new EasyModeHandler(EasyModeJson))
            { BaseAddress = new Uri("https://example.com/") };
        var httpFactory = TestDependencies.HttpFactory(httpClient);

        var easyModeManager = new EasyModeManager(logger.Object, config, httpFactory.Object, logger.Object);
        var resourceProvider = TestDependencies.ResourceProvider().Object;

        var downloadManager = new DownloadManager(
            httpFactory.Object,
            (extraction ?? new Mock<IExtractionService>()).Object,
            logger.Object,
            resourceProvider,
            new AvaloniaDispatcherService());

        return new EasyModeViewModel(easyModeManager, downloadManager, messageBox.Object, logger.Object, config,
            playSound);
    }

    [Fact]
    public async Task SelectingSystemWithImagePacks_EnablesImagePackDownloadButtons()
    {
        var vm = CreateVm();
        try
        {
            await vm.LoadCommand.ExecuteAsync(null);
            vm.SelectedSystem = vm.Systems.FirstOrDefault(s =>
                string.Equals(s.SystemName, "NES", StringComparison.OrdinalIgnoreCase));

            // Available packs (link + extract path) are shown and enabled for download.
            Assert.True(vm.IsImagePack1Available);
            Assert.True(vm.IsImagePack2Available);
            Assert.False(vm.IsImagePack1Downloaded);
            Assert.False(vm.IsImagePack2Downloaded);

            // No third pack on the NES system → the button stays hidden.
            Assert.False(vm.IsImagePack3Available);
            Assert.True(vm.IsImagePack3Downloaded);
        }
        finally
        {
            vm.Dispose();
        }
    }

    [Fact]
    public async Task SelectingSystemWithoutImagePacks_HidesImagePackButtons()
    {
        var vm = CreateVm();
        try
        {
            await vm.LoadCommand.ExecuteAsync(null);
            vm.SelectedSystem = vm.Systems.FirstOrDefault(s =>
                string.Equals(s.SystemName, "NoPackSystem", StringComparison.OrdinalIgnoreCase));

            Assert.False(vm.IsImagePack1Available);
            Assert.False(vm.IsImagePack2Available);
            Assert.False(vm.IsImagePack3Available);
            Assert.False(vm.IsImagePack4Available);
            Assert.False(vm.IsImagePack5Available);
            Assert.True(vm.IsImagePack1Downloaded);
        }
        finally
        {
            vm.Dispose();
        }
    }

    [Fact]
    public async Task ClearSelection_DisablesAllDownloadButtons()
    {
        var vm = CreateVm();
        try
        {
            await vm.LoadCommand.ExecuteAsync(null);
            vm.SelectedSystem = vm.Systems.FirstOrDefault(s =>
                string.Equals(s.SystemName, "NES", StringComparison.OrdinalIgnoreCase));
            Assert.True(vm.IsImagePack1Available);

            vm.SelectedSystem = null;

            Assert.False(vm.IsImagePack1Available);
            Assert.True(vm.IsImagePack1Downloaded);
        }
        finally
        {
            vm.Dispose();
        }
    }

    [Fact]
    public async Task Extraction_ShowsLoadingOverlayAndTiesProgressToTheBar()
    {
        var extractionStarted = new TaskCompletionSource();
        var extractionRelease = new TaskCompletionSource();

        var extraction = new Mock<IExtractionService>();
        extraction.Setup(e => e.ExtractToFolderAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(async () =>
            {
                extractionStarted.SetResult();
                await extractionRelease.Task;
                return true;
            });

        var vm = CreateVm(extraction);
        try
        {
            await vm.LoadCommand.ExecuteAsync(null);
            vm.SelectedSystem = vm.Systems.FirstOrDefault(s =>
                string.Equals(s.SystemName, "NES", StringComparison.OrdinalIgnoreCase));
            Assert.False(vm.IsImagePack1Downloaded);

            var downloadTask = vm.DownloadImagePack1Command.ExecuteAsync(null);

            // While the archive is being extracted the loading overlay is visible with
            // the extraction message and the progress bar is reset to the start (WPF parity).
            await extractionStarted.Task;
            await HeadlessAvalonia.WaitUntilAsync(() =>
                vm.IsLoading && vm.LoadingMessage.Contains("Extracting", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0.0, vm.DownloadProgress, 3);
            Assert.Contains("Extracting", vm.LoadingMessage, StringComparison.OrdinalIgnoreCase);

            extractionRelease.SetResult();

            await downloadTask;

            Assert.False(vm.IsLoading);
            Assert.False(vm.IsOperationInProgress);
            Assert.True(vm.IsImagePack1Downloaded);
        }
        finally
        {
            if (!extractionRelease.Task.IsCompleted) extractionRelease.SetResult();
            vm.Dispose();
        }
    }

    /// <summary>
    ///     Serves both the EasyMode API (JSON) and component archives (fake bytes).
    /// </summary>
    private sealed class EasyModeHandler : HttpMessageHandler
    {
        private readonly string _json;

        public EasyModeHandler(string json)
        {
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.AbsoluteUri ?? "";
            if (url.Contains("api/Systems", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_json, Encoding.UTF8, "application/json")
                });
            }

            // Component downloads (emulator / core / image pack archives).
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("fake archive bytes"u8.ToArray())
            });
        }
    }
}