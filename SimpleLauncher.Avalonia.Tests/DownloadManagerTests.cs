using System.Net;
using Moq;
using SimpleLauncher.Avalonia.Services.AvaloniaServices;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.DownloadService;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the DownloadManager install step: archive downloads go through the
///     extraction service, while single-file emulator downloads (Linux .AppImage/.run/.sh)
///     are installed directly into the destination folder (bug #67537).
/// </summary>
public class DownloadManagerTests
{
    private static DownloadManager CreateManager(byte[] payload, out Mock<IExtractionService> extraction)
    {
        HeadlessAvalonia.EnsureInitialized();

        var httpClient = TestDependencies.HttpClientWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
        });

        extraction = new Mock<IExtractionService>();
        return new DownloadManager(
            TestDependencies.HttpFactory(httpClient).Object,
            extraction.Object,
            TestDependencies.Logger().Object,
            TestDependencies.ResourceProvider().Object,
            new AvaloniaDispatcherService());
    }

    private static DownloadManager CreateStreamingManager(Func<HttpContent> contentFactory)
    {
        HeadlessAvalonia.EnsureInitialized();

        var httpClient = TestDependencies.HttpClientWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = contentFactory()
        });

        return new DownloadManager(
            TestDependencies.HttpFactory(httpClient).Object,
            new Mock<IExtractionService>().Object,
            TestDependencies.Logger().Object,
            TestDependencies.ResourceProvider().Object,
            new AvaloniaDispatcherService());
    }

    private static void DeleteTempDownload(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Theory]
    [InlineData("azahar.AppImage", true)]
    [InlineData("duckstation.run", true)]
    [InlineData("retroarch.sh", true)]
    [InlineData("emulator.zip", false)]
    [InlineData("dosbox-staging-linux-x86_64-v0.83.0.tar.xz", false)]
    [InlineData("retroarch", false)]
    public void IsSingleFileExecutableDownload_ClassifiesDownloadedFiles(string fileName, bool expected)
    {
        Assert.Equal(expected, DownloadManager.IsSingleFileExecutableDownload(fileName));
    }

    [Fact]
    public async Task ExtractFileAsync_InstallsAppImageDirectlyAndSetsExecuteBits()
    {
        var payload = "fake appimage bytes"u8.ToArray();
        using var manager = CreateManager(payload, out var extraction);
        var destination = Directory.CreateTempSubdirectory("sl-appimage-install-");
        string? downloaded = null;
        try
        {
            downloaded = await manager.DownloadFileAsync("https://example.com/azahar.AppImage");
            Assert.NotNull(downloaded);

            var result = await manager.ExtractFileAsync(downloaded, destination.FullName);

            Assert.True(result);

            // The extraction service must not be asked to unpack a non-archive (bug #67537).
            extraction.Verify(e => e.ExtractToFolderAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            var installed = Path.Combine(destination.FullName, "azahar.AppImage");
            Assert.True(File.Exists(installed));
            Assert.Equal(payload, await File.ReadAllBytesAsync(installed));

            if (!OperatingSystem.IsWindows())
            {
                // AppImages are launched directly, so the install must add the execute bits.
                var mode = File.GetUnixFileMode(installed);
                Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
                Assert.True(mode.HasFlag(UnixFileMode.GroupExecute));
                Assert.True(mode.HasFlag(UnixFileMode.OtherExecute));
            }
        }
        finally
        {
            DeleteTempDownload(downloaded);
            destination.Delete(true);
        }
    }

    [Fact]
    public async Task ExtractFileAsync_ExtractsArchiveDownloads()
    {
        var payload = "fake zip bytes"u8.ToArray();
        using var manager = CreateManager(payload, out var extraction);
        var destination = Directory.CreateTempSubdirectory("sl-archive-extract-");
        string? downloaded = null;
        try
        {
            downloaded = await manager.DownloadFileAsync("https://example.com/emulator.zip");
            Assert.NotNull(downloaded);

            extraction.Setup(e => e.ExtractToFolderAsync(downloaded, destination.FullName)).ReturnsAsync(true);

            var result = await manager.ExtractFileAsync(downloaded, destination.FullName);

            Assert.True(result);
            extraction.Verify(e => e.ExtractToFolderAsync(downloaded, destination.FullName), Times.Once);
        }
        finally
        {
            DeleteTempDownload(downloaded);
            destination.Delete(true);
        }
    }

    [Fact]
    public async Task ExtractFileAsync_UnsupportedArchiveContainerIsNotInstalledDirectly()
    {
        var payload = "fake tar.xz bytes"u8.ToArray();
        using var manager = CreateManager(payload, out var extraction);
        var destination = Directory.CreateTempSubdirectory("sl-tar-extract-");
        string? downloaded = null;
        try
        {
            downloaded = await manager.DownloadFileAsync(
                "https://example.com/dosbox-staging-linux-x86_64-v0.83.0.tar.xz");
            Assert.NotNull(downloaded);

            extraction.Setup(e => e.ExtractToFolderAsync(downloaded, destination.FullName)).ReturnsAsync(false);

            var result = await manager.ExtractFileAsync(downloaded, destination.FullName);

            Assert.False(result);

            // Compressed TAR wrappers are archives (even though extraction does not support
            // them yet): they must never be copied into the emulator folder as-is.
            extraction.Verify(e => e.ExtractToFolderAsync(downloaded, destination.FullName), Times.Once);
            Assert.False(File.Exists(Path.Combine(destination.FullName,
                "dosbox-staging-linux-x86_64-v0.83.0.tar.xz")));
        }
        finally
        {
            DeleteTempDownload(downloaded);
            destination.Delete(true);
        }
    }

    [Fact]
    public async Task DownloadFileAsync_UserCancelDeletesThePartialFile()
    {
        var stream = new StallingStream(prefixBytes: 4096);
        using var manager = CreateStreamingManager(() => new RawStreamContent(stream));
        var tempFile = Path.Combine(manager.TempFolder, "cancel-me.zip");
        try
        {
            var download = manager.DownloadFileAsync("https://example.com/cancel-me.zip");
            await stream.WaitUntilStalledAsync();
            Assert.True(File.Exists(tempFile), "the download should have written a partial file");

            manager.CancelDownload();
            var result = await download;

            Assert.Null(result);
            Assert.False(File.Exists(tempFile), "a user cancel must remove the partial download (BUG-07)");
        }
        finally
        {
            DeleteTempDownload(tempFile);
        }
    }

    [Fact]
    public async Task DownloadFileAsync_StalledBodyFailsAfterRetriesAndCleansUp()
    {
        using var manager = CreateStreamingManager(
            () => new RawStreamContent(new StallingStream(prefixBytes: 4096)));
        manager.StallTimeout = TimeSpan.FromMilliseconds(150);
        var tempFile = Path.Combine(manager.TempFolder, "stalled.zip");
        try
        {
            var result = await manager.DownloadFileAsync("https://example.com/stalled.zip");

            Assert.Null(result);
            Assert.False(File.Exists(tempFile), "a stalled download must clean up its partial file (BUG-08)");
        }
        finally
        {
            DeleteTempDownload(tempFile);
        }
    }

    /// <summary>
    ///     Emits a fixed prefix and then blocks until the read is canceled, simulating a
    ///     connection that dies while the response body is still streaming.
    /// </summary>
    private sealed class StallingStream(int prefixBytes) : Stream
    {
        private readonly TaskCompletionSource _stalled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _position;

        public Task WaitUntilStalledAsync()
        {
            return _stalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_position < prefixBytes)
            {
                var count = Math.Min(buffer.Length, prefixBytes - _position);
                buffer.Span[..count].Fill(0x41);
                _position += count;
                return count;
            }

            _stalled.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RawStreamContent(Stream stream) : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult(stream);
        }

        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context)
        {
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
