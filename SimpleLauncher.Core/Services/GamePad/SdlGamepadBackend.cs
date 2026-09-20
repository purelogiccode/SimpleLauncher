#if !WINDOWS
using Hexa.NET.SDL2;

namespace SimpleLauncher.Core.Services.GamePad;

/// <summary>
///     Controller backend for Linux/macOS built on SDL2's GameController API. SDL is
///     initialized and polled on a dedicated background thread (SDL input functions are not
///     thread-safe) and every poll reports a <see cref="GamepadInputState" /> snapshot.
///     Devices that SDL recognizes as standard game controllers (Xbox, PlayStation, Switch,
///     Steam virtual pads) use the mapped GameController API; unknown pads fall back to the
///     raw joystick API with the common evdev axis layout.
/// </summary>
internal sealed unsafe class SdlGamepadBackend : IDisposable
{
    private const int PollIntervalMilliseconds = 16;
    private const int ReconnectIntervalMilliseconds = 5000;
    private const float MaxAxisValue = 32767f;

    private const byte HatUp = 1;
    private const byte HatRight = 2;
    private const byte HatDown = 4;
    private const byte HatLeft = 8;

    private readonly Lock _gate = new();
    private readonly Action<string?> _onDeviceChanged;
    private readonly Action<string> _onError;
    private readonly Action<GamepadInputState> _onState;
    private readonly ManualResetEventSlim _stopped = new(true);

    private volatile bool _disposed;
    private volatile bool _stopRequested;
    private Thread? _thread;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SdlGamepadBackend" /> class.
    /// </summary>
    /// <param name="onState">Invoked on the polling thread for every controller state snapshot.</param>
    /// <param name="onError">Invoked with a message when the backend reports an unexpected error.</param>
    /// <param name="onDeviceChanged">Invoked with the device name on connect, or <c>null</c> on disconnect.</param>
    public SdlGamepadBackend(Action<GamepadInputState> onState, Action<string> onError,
        Action<string?> onDeviceChanged)
    {
        _onState = onState ?? throw new ArgumentNullException(nameof(onState));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _onDeviceChanged = onDeviceChanged ?? throw new ArgumentNullException(nameof(onDeviceChanged));
    }

    /// <summary>
    ///     Starts the SDL polling thread. Calling this while already running is a no-op.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SdlGamepadBackend));
            if (_thread is not null) return;

            _stopRequested = false;
            _stopped.Reset();
            _thread = new Thread(RunLoop)
            {
                IsBackground = true,
                Name = "SimpleLauncher.SdlGamepad",
            };
            _thread.Start();
        }
    }

    /// <summary>
    ///     Signals the polling thread to stop and waits (bounded) for it to release SDL.
    /// </summary>
    public void Stop()
    {
        Thread? thread;
        lock (_gate)
        {
            thread = _thread;
            if (thread is null) return;

            _stopRequested = true;
        }

        if (!thread.Join(TimeSpan.FromSeconds(2)))
        {
            // Keep the reference: a live thread must not be replaced by a second SDL session.
            _onError("The SDL gamepad polling thread did not stop within 2 seconds.");
            return;
        }

        lock (_gate)
        {
            if (ReferenceEquals(_thread, thread)) _thread = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _disposed = true;
        _stopped.Dispose();
    }

    private void RunLoop()
    {
        SDLGameController* controller = null;
        SDLJoystick* joystick = null;
        try
        {
            if (SDL.Init(SDL.SDL_INIT_GAMECONTROLLER | SDL.SDL_INIT_EVENTS) != 0)
            {
                _onError($"SDL could not be initialized ({SDL.GetErrorS()}). Gamepad navigation is unavailable.");
                return;
            }

            SDL.SetHint(SDL.SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");

            var nextReconnect = DateTime.MinValue;
            while (!_stopRequested)
            {
                try
                {
                    SDL.PumpEvents();
                    CloseDetachedDevices(ref controller, ref joystick);

                    if (controller is null && joystick is null && DateTime.UtcNow >= nextReconnect)
                    {
                        nextReconnect = DateTime.UtcNow.AddMilliseconds(ReconnectIntervalMilliseconds);
                        TryOpenDevice(ref controller, ref joystick);
                    }

                    if (controller is not null)
                    {
                        SDL.GameControllerUpdate();
                        _onState(ReadGameControllerState(controller));
                    }
                    else if (joystick is not null)
                    {
                        SDL.JoystickUpdate();
                        _onState(ReadJoystickState(joystick));
                    }
                }
                catch (Exception ex)
                {
                    _onError($"SDL gamepad polling failed: {ex.Message}");
                    CloseDetachedDevices(ref controller, ref joystick, force: true);
                }

                Thread.Sleep(PollIntervalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _onError($"The SDL gamepad backend stopped unexpectedly: {ex.Message}");
        }
        finally
        {
            CloseDetachedDevices(ref controller, ref joystick, force: true);
            SDL.Quit();
            _stopped.Set();
        }
    }

    private void CloseDetachedDevices(ref SDLGameController* controller, ref SDLJoystick* joystick,
        bool force = false)
    {
        if (controller is not null && (force || SDL.GameControllerGetAttached(controller) == SDLBool.False))
        {
            SDL.GameControllerClose(controller);
            controller = null;
            _onDeviceChanged(null);
        }

        if (joystick is not null && (force || SDL.JoystickGetAttached(joystick) == SDLBool.False))
        {
            SDL.JoystickClose(joystick);
            joystick = null;
            _onDeviceChanged(null);
        }
    }

    private void TryOpenDevice(ref SDLGameController* controller, ref SDLJoystick* joystick)
    {
        var count = SDL.NumJoysticks();
        for (var i = 0; i < count; i++)
        {
            if (SDL.IsGameController(i) == SDLBool.False) continue;

            var opened = SDL.GameControllerOpen(i);
            if (opened is null) continue;

            controller = opened;
            _onDeviceChanged(SDL.GameControllerNameForIndexS(i) ?? "Game controller");
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var opened = SDL.JoystickOpen(i);
            if (opened is null) continue;

            joystick = opened;
            _onDeviceChanged(SDL.JoystickNameForIndexS(i) ?? "Joystick");
            return;
        }
    }

    private static GamepadInputState ReadGameControllerState(SDLGameController* controller)
    {
        return new GamepadInputState(
            Normalize(SDL.GameControllerGetAxis(controller, SDLGameControllerAxis.Leftx)),
            -Normalize(SDL.GameControllerGetAxis(controller, SDLGameControllerAxis.Lefty)),
            Normalize(SDL.GameControllerGetAxis(controller, SDLGameControllerAxis.Rightx)),
            -Normalize(SDL.GameControllerGetAxis(controller, SDLGameControllerAxis.Righty)),
            IsPressed(SDL.GameControllerGetButton(controller, SDLGameControllerButton.DpadUp)),
            IsPressed(SDL.GameControllerGetButton(controller, SDLGameControllerButton.DpadDown)),
            IsPressed(SDL.GameControllerGetButton(controller, SDLGameControllerButton.DpadLeft)),
            IsPressed(SDL.GameControllerGetButton(controller, SDLGameControllerButton.DpadRight)),
            IsPressed(SDL.GameControllerGetButton(controller, SDLGameControllerButton.A)),
            IsPressed(SDL.GameControllerGetButton(controller, SDLGameControllerButton.B)));
    }

    private static GamepadInputState ReadJoystickState(SDLJoystick* joystick)
    {
        var hat = SDL.JoystickGetHat(joystick, 0);
        return new GamepadInputState(
            Normalize(SDL.JoystickGetAxis(joystick, 0)),
            -Normalize(SDL.JoystickGetAxis(joystick, 1)),
            Normalize(SDL.JoystickGetAxis(joystick, 3)),
            -Normalize(SDL.JoystickGetAxis(joystick, 4)),
            (hat & HatUp) != 0,
            (hat & HatDown) != 0,
            (hat & HatLeft) != 0,
            (hat & HatRight) != 0,
            IsPressed(SDL.JoystickGetButton(joystick, 0)),
            IsPressed(SDL.JoystickGetButton(joystick, 1)));
    }

    private static float Normalize(short value)
    {
        return Math.Clamp(value / MaxAxisValue, -1f, 1f);
    }

    private static bool IsPressed(byte value)
    {
        return value != 0;
    }
}
#endif
