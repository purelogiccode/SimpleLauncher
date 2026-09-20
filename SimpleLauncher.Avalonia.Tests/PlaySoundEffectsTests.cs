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