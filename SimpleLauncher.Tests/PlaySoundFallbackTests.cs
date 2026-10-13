using NAudio.Wave;
using SimpleLauncher.Core.Services.PlaySound;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Regression guard for bugs 67862-67879: Windows images without Media Foundation
///     (<c>mfplat.dll</c>) must still play the built-in UI sounds through the fully managed
///     fallback decoders (MP3 via NLayer, WAV via NAudio), which this Windows build now
///     ships. The Media Foundation failure itself is classified as an expected environment
///     condition by the playback loop.
/// </summary>
public sealed class PlaySoundFallbackTests
{
    /// <summary>The managed MP3 decoder exists on the Windows build and decodes the shipped sound.</summary>
    [Fact]
    public void CreateManagedReaderDecodesShippedMp3()
    {
        var mp3Path = Path.Combine(AppContext.BaseDirectory, "audio", "click.mp3");
        Assert.True(File.Exists(mp3Path), $"Missing test fixture: {mp3Path}");

        using var reader = PlaySoundEffects.CreateManagedReader(mp3Path);

        Assert.IsType<Mp3FileReaderBase>(reader);
        Assert.True(reader.WaveFormat.SampleRate > 0, "decoded format has no sample rate");
    }

    /// <summary>The managed WAV decoder needs no OS codec either.</summary>
    [Fact]
    public void CreateManagedReaderDecodesWav()
    {
        var wavPath = Path.Combine(Path.GetTempPath(), $"sl-managed-sound-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WaveFileWriter(wavPath, new WaveFormat(8000, 16, 1)))
            {
                writer.Write(new byte[1600], 0, 1600);
            }

            using var reader = PlaySoundEffects.CreateManagedReader(wavPath);

            Assert.IsType<WaveFileReader>(reader);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    /// <summary>
    ///     A missing native Media Foundation module (directly or wrapped) triggers the
    ///     fallback; unrelated decoder errors do not.
    /// </summary>
    [Fact]
    public void IsMediaFoundationUnavailableDetectsMissingNativeRuntime()
    {
        Assert.True(PlaySoundEffects.IsMediaFoundationUnavailable(
            new DllNotFoundException(
                "Unable to load DLL 'mfplat.dll' or one of its dependencies: The specified module could not be found. (0x8007007E)")));
        Assert.True(PlaySoundEffects.IsMediaFoundationUnavailable(
            new TypeInitializationException("NAudio.MediaFoundation.MediaFoundationApi",
                new DllNotFoundException("Unable to load DLL 'mfreadwrite.dll'"))));
        Assert.True(PlaySoundEffects.IsMediaFoundationUnavailable(new FileNotFoundException("mfplat.dll")));

        Assert.False(PlaySoundEffects.IsMediaFoundationUnavailable(new InvalidDataException("corrupt sound file")));
        Assert.False(PlaySoundEffects.IsMediaFoundationUnavailable(new InvalidOperationException("real bug")));
    }
}