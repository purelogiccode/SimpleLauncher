using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using NAudio.SoundFile;
using NAudio.Wave;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.PlaySound;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="PlaySoundEffects" /> (NAudio 3 playback: Media Foundation +
///     WaveOut on Windows; managed WAV/MP3 decoders or libsndfile + ALSA on Linux). Actual
///     device playback can only be attempted on a machine with an audio device, so the test
///     asserts graceful (non-throwing) behavior — headless CI and WSL2 have no audio device.
/// </summary>
public class PlaySoundEffectsTests
{
    [Fact]
    public async Task PlaySoundEffects_OnLinux_DoesNotThrow()
    {
        if (OperatingSystem.IsWindows()) return; // Windows path is exercised by the desktop app, not CI

        var settings = new SettingsManagerService(
            new ConfigurationBuilder().Build(),
            new Mock<ILogger>().Object,
            new Mock<ICredentialProtector>().Object);
        settings.EnableNotificationSound = true;

        var player = new PlaySoundEffects(settings, new Mock<ILogger>().Object);

        try
        {
            // Must not throw whether or not an ALSA device exists (headless CI, WSL2),
            // and whether or not the sound file exists.
            player.PlayNotificationSound();
            player.PlayShutterSound();
            player.PlayTrashSound();

            await Task.Delay(500); // allow background playback attempts to settle
        }
        finally
        {
            player.Dispose();
        }
    }

    /// <summary>
    ///     Regression guard for the UI freeze: a wedged audio backend (Stop() blocking, as
    ///     the PipeWire ALSA plugin does under a rapid click storm) must never block the
    ///     calling thread — playback lifecycle calls belong to the background audio thread.
    /// </summary>
    [Fact]
    public async Task PlaySound_WhenAudioStopBlocks_DoesNotBlockCallerOrDispose()
    {
        var settings = new SettingsManagerService(
            new ConfigurationBuilder().Build(),
            new Mock<ILogger>().Object,
            new Mock<ICredentialProtector>().Object);
        settings.EnableNotificationSound = true;

        using var stopEntered = new ManualResetEventSlim(false);
        using var stopRelease = new ManualResetEventSlim(false);

        var blockingPlayer = new Mock<IWavePlayer>();
        blockingPlayer.Setup(p => p.Stop()).Callback(() =>
        {
            // ReSharper disable once AccessToDisposedClosure
            stopEntered.Set();
            // ReSharper disable once AccessToDisposedClosure
            stopRelease.Wait(TimeSpan.FromSeconds(20));
        });

        var player = new PlaySoundEffects(settings, new Mock<ILogger>().Object);
        player.PlayerFactory = () => blockingPlayer.Object;

        try
        {
            // First sound: the worker starts playback through the fake player.
            player.PlayNotificationSound();
            await Task.Delay(300);

            // Second, different sound: the worker now stops the previous player, which
            // wedges inside Stop() exactly like the real ALSA/PipeWire backend.
            player.PlayShutterSound();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!stopEntered.IsSet && DateTime.UtcNow < deadline) await Task.Delay(20);
            Assert.True(stopEntered.IsSet, "audio worker never reached the blocking Stop()");

            // The caller keeps working and shutdown never joins the wedged worker.
            var sw = Stopwatch.StartNew();
            player.PlayNotificationSound();
            player.PlayConfiguredSound("trash.mp3");
            player.Dispose();
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"caller blocked for {sw.ElapsedMilliseconds}ms on a wedged audio stop");
        }
        finally
        {
            stopRelease.Set();
        }
    }

    [Fact]
    public void IsExpectedPlaybackFailure_ClassifiesEnvironmentAndUserConditions()
    {
        Assert.True(PlaySoundEffects.IsExpectedPlaybackFailure(new DllNotFoundException("libsndfile.so.1")));
        Assert.True(PlaySoundEffects.IsExpectedPlaybackFailure(
            new TypeInitializationException("NAudio.Wave.Alsa.AlsaOut", new DllNotFoundException("libasound.so.2"))));
        Assert.True(PlaySoundEffects.IsExpectedPlaybackFailure(new FileNotFoundException("libasound.so.2")));
        Assert.True(PlaySoundEffects.IsExpectedPlaybackFailure(new PlatformNotSupportedException()));
        Assert.True(PlaySoundEffects.IsExpectedPlaybackFailure(new SoundFileException("unsupported format", -1)));
        Assert.True(PlaySoundEffects.IsExpectedPlaybackFailure(new InvalidDataException("garbage")));
        Assert.True(PlaySoundEffects.IsExpectedPlaybackFailure(new EndOfStreamException()));
        Assert.True(PlaySoundEffects.IsExpectedPlaybackFailure(new FormatException()));

        Assert.False(PlaySoundEffects.IsExpectedPlaybackFailure(new InvalidOperationException("real bug")));
        Assert.False(PlaySoundEffects.IsExpectedPlaybackFailure(new NullReferenceException()));

        // A TypeInitializationException is only an environment issue when an inner exception
        // says so; a defect inside a static initializer must stay at Error and be reported.
        Assert.False(PlaySoundEffects.IsExpectedPlaybackFailure(
            new TypeInitializationException("Audio", new NullReferenceException("static initializer defect"))));
    }

    [Fact]
    public void CreateReader_UsesManagedDecodersForMp3AndWav()
    {
        // This assembly always compiles Core's Linux TFM, so MP3/WAV must decode through
        // the fully managed NLayer/WaveFileReader path — no libsndfile required.
        var mp3Path = Path.Combine(AppContext.BaseDirectory, "audio", "click.mp3");
        using (var mp3Reader = PlaySoundEffects.CreateReader(mp3Path))
        {
            Assert.IsType<Mp3FileReaderBase>(mp3Reader);
        }

        var wavPath = Path.Combine(Path.GetTempPath(), $"sl-sound-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WaveFileWriter(wavPath, new WaveFormat(8000, 16, 1)))
            {
                writer.Write(new byte[1600], 0, 1600);
            }

            using var wavReader = PlaySoundEffects.CreateReader(wavPath);
            Assert.IsType<WaveFileReader>(wavReader);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    [Fact]
    public async Task PlayConfiguredSound_WithCorruptMp3_LogsInformationInsteadOfError()
    {
        var settings = new SettingsManagerService(
            new ConfigurationBuilder().Build(),
            new Mock<ILogger>().Object,
            new Mock<ICredentialProtector>().Object);

        var logger = new Mock<ILogger>();
        var player = new PlaySoundEffects(settings, logger.Object);

        var corruptPath = Path.Combine(Path.GetTempPath(), $"sl-corrupt-{Guid.NewGuid():N}.mp3");
        await File.WriteAllBytesAsync(corruptPath, [1, 3, 3, 7]);

        try
        {
            player.PlayConfiguredSound(corruptPath);
            await Task.Delay(200);
        }
        finally
        {
            player.Dispose();
            File.Delete(corruptPath);
        }

        logger.Verify(l => l.Information(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
    }
}