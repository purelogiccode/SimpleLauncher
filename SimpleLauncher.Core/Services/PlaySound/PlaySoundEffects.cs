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
///     Playback runs on a dedicated background thread: callers only validate and enqueue
///     the request. A wedged audio backend (e.g. the PipeWire ALSA plugin blocking inside
///     <c>snd_pcm_mmap_writei</c>) can therefore silence sound effects but can never freeze
///     the UI or block shutdown. The previous design called <c>Stop()</c>/<c>Dispose()</c>
///     (which join the playback thread) synchronously on the UI thread, so a single wedged
///     stop froze the whole application (rapid aspect-ratio clicks reproduced it).
/// </summary>
public class PlaySoundEffects : IPlaySoundEffects, IDisposable
{
    private const string ClickSoundFile = "click.mp3";
    private const string ShutterSoundFile = "shutter.mp3";
    private const string TrashSoundFile = "trash.mp3";

    // Repeated requests for the same sound within this window are coalesced: a rapid
    // click storm must not restart the ALSA stream dozens of times per second (that
    // churn is what wedges the PipeWire backend in the first place).
    private static readonly TimeSpan DuplicateSoundThrottle = TimeSpan.FromMilliseconds(250);

    private readonly ILogger _logger;
    private readonly SettingsManagerService _settingsManager;

    // Playback state is owned exclusively by the audio thread; the request handoff is
    // guarded by this lock. The newest pending request wins (bursts coalesce).
    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _requestSignal = new(0, 1);
    private Thread? _audioThread;
    private string? _pendingSoundPath;
    private string? _lastStartedSoundPath;
    private DateTime _lastStartedUtc;
    private volatile bool _disposed;

    private IWavePlayer? _player;
    private WaveStream? _reader;

    /// <summary>
    ///     Test seam: creates the output device. Production uses <see cref="CreatePlayer" />;
    ///     tests substitute a fake whose Stop() blocks to prove that the playback lifecycle
    ///     never runs on the caller's thread.
    /// </summary>
    internal Func<IWavePlayer> PlayerFactory { get; set; } = CreatePlayer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PlaySoundEffects" /> class.
    /// </summary>
    public PlaySoundEffects(SettingsManagerService settings, ILogger logger)
    {
        _settingsManager = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Stops playback and releases audio resources. Never joins the audio thread:
    ///     a wedged audio backend must not block application shutdown (the thread is a
    ///     background thread, so it cannot keep the process alive either).
    /// </summary>
    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        // Wake the audio thread so it can stop playback and exit.
        try
        {
            _requestSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A request is already pending; the wake-up is already guaranteed.
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
            lock (_stateLock)
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
                "Attempted to play sound with an empty filename.");
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

        lock (_stateLock)
        {
            if (_disposed) return;
            _pendingSoundPath = soundPath;
        }

        EnsureAudioThread();

        try
        {
            _requestSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A request is already pending; the newest path wins.
        }
    }

    private void EnsureAudioThread()
    {
        if (_audioThread is not null) return;

        lock (_stateLock)
        {
            if (_audioThread is not null) return;

            var thread = new Thread(AudioLoop)
            {
                IsBackground = true,
                Name = "SimpleLauncher Audio"
            };
            _audioThread = thread;
            thread.Start();
        }
    }

    /// <summary>
    ///     Audio worker: consumes the latest pending request, stops the previous playback
    ///     and starts the new one. Every NAudio/ALSA call happens here — including the
    ///     stop/dispose that joins the playback thread — so a wedged backend only stalls
    ///     this background thread.
    /// </summary>
    private void AudioLoop()
    {
        while (true)
        {
            try
            {
                _requestSignal.Wait();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            bool disposed;
            string? soundPath;
            lock (_stateLock)
            {
                disposed = _disposed;
                soundPath = _pendingSoundPath;
                _pendingSoundPath = null;
            }

            if (disposed)
            {
                // Best effort: stop the current sound before the thread exits. If the
                // backend is wedged this hangs only this background thread.
                StopCurrentPlayback();
                return;
            }

            if (soundPath is null) continue;

            try
            {
                if (IsDuplicateWithinThrottle(soundPath)) continue;

                StopCurrentPlayback();

                _reader = CreateReader(soundPath);
                _player = PlayerFactory();
                _player.Init(_reader);
                _player.Play();

                lock (_stateLock)
                {
                    _lastStartedSoundPath = soundPath;
                    _lastStartedUtc = DateTime.UtcNow;
                }
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

    private bool IsDuplicateWithinThrottle(string soundPath)
    {
        lock (_stateLock)
        {
            return string.Equals(_lastStartedSoundPath, soundPath, StringComparison.Ordinal) &&
                   DateTime.UtcNow - _lastStartedUtc < DuplicateSoundThrottle;
        }
    }

    /// <summary>
    ///     Whether a sound playback failure is an expected environment/user condition rather
    ///     than a defect: a missing native decoder/output library, no usable audio device, or an
    ///     unsupported/corrupt sound file. Inner exceptions are inspected too, because
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
}
