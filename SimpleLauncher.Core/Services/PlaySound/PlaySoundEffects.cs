// ReSharper disable once RedundantUsingDirective
using NAudio;
using NAudio.Wave;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;
using NAudio.SoundFile;
// ReSharper disable once RedundantUsingDirective
using NAudio.Wave.Alsa;
#if !WINDOWS
using NLayer.NAudioSupport;
#endif

namespace SimpleLauncher.Core.Services.PlaySound;

/// <summary>
///     Plays UI sound effects such as click, shutter, and trash sounds using NAudio 3.
///     Decoding and output are platform-specific (Windows: Media Foundation + WaveOut;
///     Linux/macOS: managed WAV/MP3 decoders or libsndfile via NAudio.SoundFile, plus ALSA
///     via NAudio.Alsa), but the playback pipeline itself is a single cross-platform path
///     built on <see cref="IWavePlayer" />.
/// </summary>
public class PlaySoundEffects : IPlaySoundEffects, IDisposable
{
    private const string ClickSoundFile = "click.mp3";
    private const string ShutterSoundFile = "shutter.mp3";
    private const string TrashSoundFile = "trash.mp3";

    private static readonly Lock Lock = new();
    private readonly ILogger _logger;
    private readonly SettingsManagerService _settingsManager;

    private IWavePlayer? _player;
    private WaveStream? _reader;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PlaySoundEffects" /> class.
    /// </summary>
    public PlaySoundEffects(SettingsManagerService settings, ILogger logger)
    {
        _settingsManager = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Stops any current playback and releases audio resources.
    /// </summary>
    public void Dispose()
    {
        lock (Lock)
        {
            StopCurrentPlayback();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Plays the configured notification sound if notifications are enabled.
    /// </summary>
    public void PlayNotificationSound()
    {
        if (!_settingsManager.EnableNotificationSound) return;

        PlaySound(_settingsManager.CustomNotificationSoundFile ?? ClickSoundFile);
    }

    /// <summary>
    ///     Plays the shutter sound effect.
    /// </summary>
    public void PlayShutterSound()
    {
        PlaySound(ShutterSoundFile);
    }

    /// <summary>
    ///     Plays the trash/delete sound effect.
    /// </summary>
    public void PlayTrashSound()
    {
        PlaySound(TrashSoundFile);
    }

    /// <summary>
    ///     Plays a sound file by name from the audio directory.
    /// </summary>
    public void PlayConfiguredSound(string soundFileName)
    {
        if (string.IsNullOrWhiteSpace(soundFileName))
        {
            lock (Lock)
            {
                _logger.Error(
                    new ArgumentNullException(nameof(soundFileName),
                        "PlayConfiguredSound called with null or empty soundFileName."),
                    "Attempted to play sound with an empty filename");
            }

            return;
        }

        PlaySound(soundFileName);
    }

    private void PlaySound(string soundFileName)
    {
        if (string.IsNullOrWhiteSpace(soundFileName))
        {
            _logger.Error(
                new ArgumentNullException(nameof(soundFileName), "Attempted to play sound with an empty filename."),
                "Attempted to play sound with an empty filename");
            return;
        }

        var soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio", soundFileName);
        if (!File.Exists(soundPath))
        {
            var contextMessageMissing = $"Sound file not found: {soundPath}";
            _logger.Error(
                new FileNotFoundException(contextMessageMissing, soundPath),
                contextMessageMissing);
            return;
        }

        lock (Lock)
        {
            StopCurrentPlayback();

            try
            {
                _reader = CreateReader(soundPath);
                _player = CreatePlayer();
                _player.PlaybackStopped += OnPlaybackStopped;
                _player.Init(_reader);
                _player.Play();
            }
            catch (Exception ex)
            {
                // Missing native dependency (libsndfile/libasound), no audio device (WSL2, CI,
                // containers, headless) or an unsupported/corrupt sound file — expected
                // environment/user conditions, not bugs, so they are logged at Information
                // level to avoid being reported to the bug-report API. Anything else is a
                // real defect and stays at Error.
                if (IsExpectedPlaybackFailure(ex))
                {
                    _logger.Information(ex, $"Failed to play sound: {soundPath}");
                }
                else
                {
                    _logger.Error(ex, $"Failed to play sound: {soundPath}");
                }

                StopCurrentPlayback();
            }
        }
    }

    /// <summary>
    ///     Whether a sound playback failure is an expected environment/user condition rather
    ///     than a defect: a missing native decoder/output library, no usable audio device, or
    ///     an unsupported/corrupt sound file. Inner exceptions are inspected too, because
    ///     P/Invoke failures are often wrapped (e.g. <see cref="TypeInitializationException" />):
    ///     such a wrapper only counts when one of its inner exceptions does, so a genuine
    ///     static-initializer defect is still reported as a bug.
    /// </summary>
    internal static bool IsExpectedPlaybackFailure(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (IsExpectedPlaybackFailureType(current)) return true;
        }

        return false;
    }

    private static bool IsExpectedPlaybackFailureType(Exception exception)
    {
#if WINDOWS
        if (exception is MmException) return true; // BadDeviceId — no wave device
#else
        if (exception is AlsaException) return true; // no ALSA device, or the device is busy
#endif
        return exception
            is DllNotFoundException // native libsndfile/libasound is not installed
            or FileNotFoundException // a native library file could not be resolved
            or PlatformNotSupportedException // macOS has no audio output backend
            or SoundFileException // libsndfile rejected the file (unsupported/truncated)
            or InvalidDataException // a managed decoder rejected the file
            or EndOfStreamException // the file ends mid-frame
            or FormatException; // malformed audio data
    }

    /// <summary>
    ///     Creates the sound decoder. Windows uses Media Foundation (built into the OS,
    ///     no extra dependencies). On Linux/macOS, WAV and MP3 use fully managed decoders
    ///     (NAudio's <see cref="WaveFileReader" /> and <see cref="Mp3FileReaderBase" /> with
    ///     NLayer), so the built-in UI sounds play without any system packages; other formats
    ///     use the system libsndfile through <see cref="SoundFileReader" />.
    /// </summary>
    internal static WaveStream CreateReader(string soundPath)
    {
#if WINDOWS
        return new MediaFoundationReader(soundPath);
#else
        var extension = Path.GetExtension(soundPath);

        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            return new Mp3FileReaderBase(soundPath, waveFormat => new Mp3FrameDecompressor(waveFormat));

        if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            return new WaveFileReader(soundPath);

        return new SoundFileReader(soundPath);
#endif
    }

    /// <summary>
    ///     Creates the output device: WaveOut (winmm) on Windows, ALSA on Linux.
    /// </summary>
    private static IWavePlayer CreatePlayer()
    {
#if WINDOWS
        return new WaveOut();
#else
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Audio playback is only supported on Windows and Linux.");

        return new AlsaOut();
#endif
    }

    private void StopCurrentPlayback()
    {
        var player = _player;
        if (player != null)
        {
            _player = null;
            player.PlaybackStopped -= OnPlaybackStopped;
            try
            {
                player.Stop();
            }
            catch (Exception ex)
            {
                _logger.Debug($"[PlaySoundEffects] Error stopping player: {ex.Message}");
            }
        }

        var reader = _reader;
        if (reader != null)
        {
            _reader = null;
            try
            {
                reader.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Debug($"[PlaySoundEffects] Error disposing reader: {ex.Message}");
            }
        }

        if (player != null)
        {
            try
            {
                player.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Debug($"[PlaySoundEffects] Error disposing player: {ex.Message}");
            }
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (Lock)
        {
            if (_player == sender) StopCurrentPlayback();
        }
    }
}