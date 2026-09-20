using Microsoft.Extensions.Configuration;
using SharpDX;
using SharpDX.DirectInput;
using SharpDX.XInput;
using SimpleLauncher.Core.Interfaces;
using WindowsInput;
using DeviceType = SharpDX.DirectInput.DeviceType;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Core.Services.GamePad;

/// <summary>
///     Manages gamepad input from Xbox (XInput) and PlayStation (DirectInput) controllers,
///     translating thumbstick and button events into mouse movement, clicks, and scrolling.
/// </summary>
public class GamePadController : IDisposable
{
    private const int RefreshRate = 60;

    // Normalize XInput values
    private const float MaxThumbValue = 32767.0f;
    private const int ReconnectDelayMilliseconds = 5000; // Delay between reconnection attempts

    private const int XInputScalingFactor = 7;
    private const int DirectInputLeftThumbStickScalingFactor = 7;
    private const int DirectInputRightThumbStickScalingFactor = 1;
    private readonly IConfiguration _configuration;
    private readonly InputSimulator _inputSimulator;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;

    private readonly IMouseSimulator _mouseSimulator;

    // System.Threading.Lock is non-reentrant: CheckAndReconnectControllers assumes
    // the caller already holds this lock and must NOT acquire it again (CORE-03).
    private readonly Lock _stateLock = new();
    private readonly Timer _timer;
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    private readonly Controller? _xinputController;

    // Non-Windows platforms run the managed SDL2 backend, which raises InputChanged
    // snapshots instead of simulating OS-level input (the WPF approach).
#if !WINDOWS
    private readonly SdlGamepadBackend? _sdlBackend;
#endif

    // DirectInput object needs to be managed for its lifetime
    private DirectInput? _directInput;
    private Joystick? _directInputController;
    private bool _isDisposed;
    private DateTime _lastReconnectAttempt = DateTime.MinValue; // Track the last reconnection attempt

    // Handle DirectInput reconnection
    private Guid _playStationControllerGuid; // Store the GUID of the connected PlayStation controller

    // For XInput
    private volatile bool _wasADown;
    private volatile bool _wasBDown;
    private volatile bool _wasCircleDown;

    // For DirectInput
    private volatile bool _wasCrossDown;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GamePadController" /> class with the specified dependencies.
    /// </summary>
    /// <param name="messageBoxLibrary">The message box service for error notifications.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public GamePadController(IMessageBoxLibraryService messageBoxLibrary, IConfiguration configuration, ILogger logger)
    {
        _messageBoxLibrary = messageBoxLibrary ?? throw new ArgumentNullException(nameof(messageBoxLibrary));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // XInput (SharpDX) and DirectInput are Windows-only APIs; their native libraries
        // (kernel32, dinput8) do not exist on Linux/macOS. Skip initialization there and
        // leave the controllers null — UpdateAsync already handles null controllers.
        if (OperatingSystem.IsWindows())
        {
            // Initialize Xbox Controller using XInput
            _xinputController = new Controller(UserIndex.One);

            // Initialize DirectInput object once
            _directInput = new DirectInput();

            // Initialize PlayStation Controller using DirectInput (find the first gamepad)
            try
            {
                var devices = _directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices);
                if (devices.Count > 0)
                {
                    _directInputController = new Joystick(_directInput, devices[0].InstanceGuid);
                    _directInputController.Acquire();
                    _playStationControllerGuid = devices[0].InstanceGuid; // Store the GUID
                }
            }
            catch (Exception ex)
            {
                // Log initialization errors but allow the application to continue
                // as XInput might still work or the controller might be connected later.
                // Notify developer
                ErrorLogger?.Invoke(ex, $"Error during initial DirectInput controller setup.\n\n" +
                                        $"Exception type: {ex.GetType().Name}\n" +
                                        $"Exception details: {ex.Message}");

                _directInputController = null; // Ensure it's null if setup failed
                _playStationControllerGuid = Guid.Empty;
            }
        }
        else
        {
            _xinputController = null;
            _directInput = null;
            _directInputController = null;
            _playStationControllerGuid = Guid.Empty;
#if !WINDOWS
            _sdlBackend = new SdlGamepadBackend(OnSdlState, OnSdlError, OnSdlDeviceChanged);
#endif
        }

        _inputSimulator = new InputSimulator();
        _mouseSimulator = _inputSimulator.Mouse;
        _timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    ///     Gets or sets the action used to log gamepad-related errors and notify the developer.
    /// </summary>
    internal Action<Exception?, string> ErrorLogger { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the dead zone for the X axis of the thumbsticks.
    /// </summary>
    internal float DeadZoneX { get; set; } = 0.05f;

    /// <summary>
    ///     Gets or sets the dead zone for the Y axis of the thumbsticks.
    /// </summary>
    internal float DeadZoneY { get; set; } = 0.02f;

    /// <summary>
    ///     Gets a value indicating whether the controller polling loop is running.
    /// </summary>
    internal bool IsRunning { get; private set; }

#if !WINDOWS
    /// <summary>
    ///     Raised on the SDL polling thread whenever the non-Windows backend reports a new
    ///     controller state (dead zone already applied). Windows uses direct input simulation
    ///     and never raises this event.
    /// </summary>
    internal event Action<GamepadInputState>? InputChanged;
#endif

    /// <summary>
    ///     Releases all resources used by the gamepad controller, including the polling timer and DirectInput devices.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Starts the gamepad polling timer and marks the controller as running.
    /// </summary>
    /// <returns>A task that completes when the controller has started, or a message box task if starting failed.</returns>
    internal Task StartAsync()
    {
        // Windows drives XInput/DirectInput with OS-level input simulation; other platforms
        // run the SDL2 backend, which reports states through InputChanged.
        if (!OperatingSystem.IsWindows())
        {
#if !WINDOWS
            return StartSdlBackendAsync();
#else
            return Task.CompletedTask;
#endif
        }

        Exception? startException = null;
        lock (_stateLock)
        {
            // Enforce proper disposal semantics: do not allow restarting a disposed instance.
            // Once disposed, a new instance must be created. This prevents resource leaks and undefined behavior.
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GamePadController),
                    "Cannot start a disposed GamePadController. A new instance must be created.");
            }

            try
            {
                _timer.Change(0, 1000 / RefreshRate);
                IsRunning = true;
            }
            catch (Exception ex)
            {
                // Notify developer
                ErrorLogger?.Invoke(ex, $"Error in GamePadController Start method.\n\n" +
                                        $"Exception type: {ex.GetType().Name}\n" +
                                        $"Exception details: {ex.Message}");
                startException = ex;
            }
        }

        // Notify user (outside lock to allow async/await)
        if (startException != null)
        {
            return _messageBoxLibrary.GamePadErrorMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Stops the gamepad polling timer and marks the controller as not running.
    /// </summary>
    /// <returns>A task that completes when the controller has stopped, or a message box task if stopping failed.</returns>
    internal Task StopAsync()
    {
        // Windows stops the input-simulation timer; other platforms stop the SDL backend.
        if (!OperatingSystem.IsWindows())
        {
#if !WINDOWS
            return StopSdlBackendAsync();
#else
            return Task.CompletedTask;
#endif
        }

        Exception? stopException = null;
        lock (_stateLock)
        {
            try
            {
                IsRunning = false;
                // Change timer to stop, but don't dispose it here
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                // Notify developer
                ErrorLogger?.Invoke(ex, $"Error in GamePadController Stop method.\n\n" +
                                        $"Exception type: {ex.GetType().Name}\n" +
                                        $"Exception details: {ex.Message}");
                stopException = ex;
            }
        }

        // Notify user (outside lock to allow async/await)
        if (stopException != null)
        {
            return _messageBoxLibrary.GamePadErrorMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }

        return Task.CompletedTask;
    }

#if !WINDOWS
    /// <summary>
    ///     Starts the SDL2 backend used on non-Windows platforms.
    /// </summary>
    private Task StartSdlBackendAsync()
    {
        Exception? startException = null;
        lock (_stateLock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GamePadController),
                    "Cannot start a disposed GamePadController. A new instance must be created.");
            }

            try
            {
                _sdlBackend?.Start();
                IsRunning = true;
                _logger.Debug("[GamePadController] SDL gamepad backend started");
            }
            catch (Exception ex)
            {
                ErrorLogger?.Invoke(ex, $"Error in GamePadController Start method.\n\n" +
                                        $"Exception type: {ex.GetType().Name}\n" +
                                        $"Exception details: {ex.Message}");
                startException = ex;
            }
        }

        // Notify user (outside lock to allow async/await)
        if (startException != null)
        {
            return _messageBoxLibrary.GamePadErrorMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Stops the SDL2 backend used on non-Windows platforms.
    /// </summary>
    private Task StopSdlBackendAsync()
    {
        Exception? stopException = null;
        lock (_stateLock)
        {
            try
            {
                IsRunning = false;
                _sdlBackend?.Stop();
                _logger.Debug("[GamePadController] SDL gamepad backend stopped");
            }
            catch (Exception ex)
            {
                ErrorLogger?.Invoke(ex, $"Error in GamePadController Stop method.\n\n" +
                                        $"Exception type: {ex.GetType().Name}\n" +
                                        $"Exception details: {ex.Message}");
                stopException = ex;
            }
        }

        // Notify user (outside lock to allow async/await)
        if (stopException != null)
        {
            return _messageBoxLibrary.GamePadErrorMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }

        return Task.CompletedTask;
    }

    private void OnSdlState(GamepadInputState rawState)
    {
        InputChanged?.Invoke(rawState.WithDeadZone(DeadZoneX, DeadZoneY));
    }

    private void OnSdlError(string message)
    {
        ErrorLogger?.Invoke(null, message);
    }

    private void OnSdlDeviceChanged(string? deviceName)
    {
        if (deviceName is null)
            _logger.Information("[GamePadController] Controller disconnected");
        else
            _logger.Information("[GamePadController] Controller connected: {DeviceName}", deviceName);
    }
#endif

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        // Step 1: Signal disposal immediately so UpdateAsync exits quickly
        // This must be inside a lock to ensure visibility across threads
        lock (_stateLock)
        {
            _isDisposed = true;
            IsRunning = false;
        }

        if (disposing)
        {
            // Step 2: Dispose timer OUTSIDE the lock to prevent deadlock
            // UpdateAsync needs _stateLock to check _isDisposed, so we must release it before waiting
            DisposeTimerSafely();

#if !WINDOWS
            // Stops the SDL polling thread and releases the SDL subsystems it initialized.
            _sdlBackend?.Dispose();
#endif

            // Step 3: Now safe to dispose other managed resources
            lock (_stateLock)
            {
                try
                {
                    _directInputController?.Unacquire();
                    _directInputController?.Dispose();
                    _directInputController = null;

                    _directInput?.Dispose();
                    _directInput = null;

                    // Fix resource leak: Dispose the semaphore
                    _updateLock?.Dispose();
                }
                catch (Exception ex)
                {
                    ErrorLogger?.Invoke(ex, "Error disposing DirectInput resources.");
                }
            }
        }
    }

    private void DisposeTimerSafely()
    {
        try
        {
            using var waitHandle = new ManualResetEvent(false);

            // Dispose(WaitHandle) already stops the timer;
            // Only wait if Dispose returns true (meaning a callback was pending and needs to signal completion).
            // If timer is null or already disposed, Dispose returns false and we skip WaitOne to prevent blocking.
            if (_timer?.Dispose(waitHandle) ?? false)
            {
                // Use a generous timeout (2-3s) to account for slower systems
                if (!waitHandle.WaitOne(TimeSpan.FromSeconds(2)))
                {
                    ErrorLogger?.Invoke(null,
                        "GamePadController timer disposal timed out. A callback may be stuck.");
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLogger?.Invoke(ex, "Error disposing GamePadController timer.");
        }
    }

    /// <summary>
    ///     Timer callback. Must stay synchronous <c>void</c> (TimerCallback signature);
    ///     it forwards to <see cref="UpdateAsync" /> fire-and-forget. All exceptions are
    ///     already observed inside <see cref="UpdateAsync" />, so the task cannot fault
    ///     unobserved — the continuation below is a backstop for future regressions (CORE-10).
    /// </summary>
    private void OnTimerTick(object? state)
    {
        _ = UpdateAsync().ContinueWith(static (t, s) =>
        {
            if (t.IsFaulted && s is GamePadController self)
                self._logger.Error(t.Exception, "Error in method UpdateAsync (timer backstop)");
        }, this, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private async Task UpdateAsync()
    {
        try
        {
            // Check disposal BEFORE attempting to wait
            lock (_stateLock)
            {
                if (_isDisposed) return;
            }

            // Skip if another update is in progress
            // Handle ObjectDisposedException if Dispose() was called just before this
            try
            {
                if (!await _updateLock.WaitAsync(0)) return;
            }
            catch (ObjectDisposedException)
            {
                return; // Shutdown in progress
            }

            try
            {
                lock (_stateLock)
                {
                    // Check if disposed or not running before processing
                    if (_isDisposed || !IsRunning) return;

                    try
                    {
                        // Check for null before accessing _xinputController
                        // Prioritize XInput if connected
                        if (_xinputController == null) return;

                        if (_xinputController.IsConnected)
                        {
                            try
                            {
                                // Handle Xbox Controller Input
                                _xinputController.GetState(out var state);
                                HandleXInputMovement(state);
                                HandleXInputScroll(state);
                                HandleXInputLeftButton(state);
                                HandleXInputRightButton(state);

                                // If XInput is connected, ensure DirectInput controller is released
                                if (_directInputController != null)
                                {
                                    _directInputController.Unacquire();
                                    _directInputController.Dispose(); // Dispose the old one
                                    _directInputController = null;
                                    _playStationControllerGuid = Guid.Empty;
                                }
                            }
                            catch (SharpDXException)
                            {
                                // Controller likely disconnected between the IsConnected check and GetState.
                                // The main loop's reconnection logic will handle it on the next tick.
                            }
                        }
                        // If XInput is not connected, try DirectInput
                        else if (_directInputController is
                                 { IsDisposed: false }) // Check if DirectInput controller exists and is not disposed
                        {
                            try
                            {
                                // Handle PlayStation Controller Input
                                var state = _directInputController.GetCurrentState();
                                HandleDirectInputMovement(state);
                                HandleDirectInputButtons(state);
                                HandleDirectInputScroll(state);
                            }
                            catch (SharpDXException ex) when
                                (ex.HResult == unchecked((int)0x8007001E)) // DIERR_INPUTLOST
                            {
                                // DirectInput device lost, attempt reconnection
                                _directInputController?.Unacquire();
                                _directInputController?.Dispose();
                                _directInputController = null;
                                _playStationControllerGuid = Guid.Empty;
                                CheckAndReconnectControllers(); // Attempt reconnection immediately
                            }
                            catch (SharpDXException ex) when
                                (ex.HResult == unchecked((int)0x8007000C)) // DIERR_NOTACQUIRED
                            {
                                // DirectInput device not acquired, attempt re-acquisition or reconnection
                                // Notify developer
                                ErrorLogger?.Invoke(ex,
                                    "DirectInput device not acquired (DIERR_NOTACQUIRED). Attempting re-acquisition/reconnection.");
                                try
                                {
                                    _directInputController?.Acquire(); // Try acquiring again
                                }
                                catch (Exception acquireEx)
                                {
                                    // Notify developer
                                    ErrorLogger?.Invoke(acquireEx,
                                        "Failed to re-acquire DirectInput device. Attempting full reconnection.");

                                    _directInputController?.Unacquire();
                                    _directInputController?.Dispose();
                                    _directInputController = null;
                                    _playStationControllerGuid = Guid.Empty;
                                    CheckAndReconnectControllers(); // Attempt full reconnection
                                }
                            }
                            catch (Exception)
                            {
                                // Catch any other exceptions during DirectInput processing
                                _directInputController?.Unacquire();
                                _directInputController?.Dispose();
                                _directInputController = null;
                                _playStationControllerGuid = Guid.Empty;
                                CheckAndReconnectControllers(); // Attempt reconnection
                            }
                        }
                        // If neither XInput nor DirectInput controller is active, attempt reconnection after delay
                        else
                        {
                            if (!((DateTime.Now - _lastReconnectAttempt).TotalMilliseconds >
                                  ReconnectDelayMilliseconds))
                            {
                                return;
                            }

                            CheckAndReconnectControllers();
                            _lastReconnectAttempt = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Check for the specific UIPI exception from InputSimulatorCore.
                        // This is a known limitation when trying to send input to a higher-privilege window.
                        // We will log it for debugging but not report it as a critical error to the user or developer.
                        if (ex.Message.Contains("User Interface Privacy Isolation (UIPI)",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.Debug($"[GamePadController] UIPI blocked input simulation: {ex.Message}");
                            // Do not call ErrorLogger or show a message box. This is an expected OS behavior.
                        }
                        else
                        {
                            // For any other unexpected exception, log it and notify the user.
                            ErrorLogger?.Invoke(ex, $"Unexpected error in GamePadController Update loop.\n\n" +
                                                    $"Exception type: {ex.GetType().Name}\n" +
                                                    $"Exception details: {ex.Message}");

                            // Notify user (fire-and-forget inside lock; exception observed via continuation)
                            _ = _messageBoxLibrary.GamePadErrorMessageBoxAsync(
                                    PathHelper.ResolveLogFilePath(_configuration))
                                .ContinueWith(static (t, state) =>
                                {
                                    if (t.IsFaulted)
                                    {
                                        (state as ILogger)?.Error(t.Exception,
                                            "Error showing GamePadErrorMessageBoxAsync");
                                    }
                                }, _logger, TaskContinuationOptions.OnlyOnFaulted);
                        }

                        // Attempt reconnection as a recovery step
                        // Do NOT call CheckAndReconnectControllers here - it will be called by the timer
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log but don't throw in async void
                ErrorLogger?.Invoke(ex, "Update loop error");
                _logger.Error(ex, "Error in method UpdateAsync");
            }
            finally
            {
                // Always release the semaphore to prevent permanent blocking.
                // Catch ObjectDisposedException if Dispose() disposed the semaphore
                // between the acquire and this release.
                try
                {
                    _updateLock.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Semaphore was disposed, ignore
                }
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            _logger.Error(ex, "Error in method UpdateAsync");
        }
    }

    /// <summary>
    ///     Checks for available gamepad controllers and attempts to reconnect a DirectInput device if disconnected.
    /// </summary>
    private void CheckAndReconnectControllers()
    {
        // XInput/DirectInput are Windows-only; nothing to reconnect elsewhere.
        if (!OperatingSystem.IsWindows()) return;

        // Caller (UpdateAsync) already holds _stateLock; do NOT acquire it again —
        // System.Threading.Lock is non-reentrant and would throw LockRecursionException (CORE-03).
        try
        {
            if (!IsRunning || _isDisposed) return;

            // If XInput is already connected, no need to reconnect DirectInput
            if (_xinputController?.IsConnected == true)
            {
                // Ensure DirectInput controller is released if XInput is active
                if (_directInputController == null) return;

                // Safely dispose DirectInput controller
                _directInputController?.Unacquire();
                _directInputController?.Dispose();
                _directInputController = null;
                _playStationControllerGuid = Guid.Empty;

                return; // XInput is active, nothing more to do
            }

            // If DirectInput object is null or disposed, try to recreate it
            if (_directInput == null || _directInput.IsDisposed)
            {
                // Recreate DirectInput object
                try
                {
                    // Dispose the old instance if it exists before creating a new one.
                    _directInput?.Dispose();

                    _directInput = new DirectInput();

                    // Notify developer
                    ErrorLogger?.Invoke(null,
                        "Recreated DirectInput object during reconnection."); // Log successful recreation
                }
                catch (Exception diEx)
                {
                    // Notify developer
                    ErrorLogger?.Invoke(diEx,
                        $"Failed to recreate DirectInput object during reconnection attempt.\n\n" +
                        $"Exception type: {diEx.GetType().Name}\n" +
                        $"Exception details: {diEx.Message}");

                    _directInput = null; // Ensure it's null if creation failed
                    return; // Cannot proceed without a valid DirectInput object
                }
            }

            // Find and reconnect DirectInput devices
            // Check if the previously connected PlayStation controller is attached
            var devices = _directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AttachedOnly);

            var found = false;
            DeviceInstance? foundDevice = null;

            // First, try to find the specific controller by GUID if we had one
            if (_playStationControllerGuid != Guid.Empty)
            {
                foreach (var deviceInstance in devices)
                {
                    if (deviceInstance.InstanceGuid != _playStationControllerGuid) continue;

                    foundDevice = deviceInstance;
                    found = true;
                    break;
                }
            }

            // If the specific GUID wasn't found or we didn't have one, just take the first available gamepad
            if (!found && devices.Count > 0)
            {
                foundDevice = devices[0];
                found = true;
            }

            if (found && foundDevice != null)
            {
                // Attempt to connect to the found device
                // Found a device, try to connect
                try
                {
                    // Dispose the old controller if it exists and is different or invalid
                    // Access InstanceGuid via the Information property of the Joystick
                    if (_directInputController != null
                        && (_directInputController.Information.InstanceGuid != foundDevice.InstanceGuid ||
                            _directInputController.IsDisposed))
                    {
                        _directInputController?.Unacquire();
                        _directInputController?.Dispose();
                        _directInputController = null;
                        _playStationControllerGuid = Guid.Empty;
                    }

                    // If _directInputController is null (either was null, different, or disposed), create a new one
                    if (_directInputController == null)
                    {
                        _directInputController = new Joystick(_directInput, foundDevice.InstanceGuid);
                        _directInputController.Acquire();
                        _playStationControllerGuid = foundDevice.InstanceGuid; // Update the GUID
                    }
                    else
                    {
                        // If it wasn't null and was the same GUID, try re-acquiring just in case
                        _directInputController.Acquire();
                    }
                }
                catch (Exception acquireEx)
                {
                    // Failed to acquire the found device
                    // Notify developer
                    ErrorLogger?.Invoke(acquireEx,
                        $"Failed to acquire DirectInput device during reconnection attempt: {foundDevice.InstanceName}.\n\n" +
                        $"Exception type: {acquireEx.GetType().Name}\n" +
                        $"Exception details: {acquireEx.Message}");

                    _directInputController?.Unacquire();
                    _directInputController?.Dispose();
                    _directInputController = null; // Ensure it's null on failure
                    _playStationControllerGuid = Guid.Empty; // Reset GUID on failure
                }
            }
            else
            {
                // No gamepad device found
                // Clean up if no device is found
                if (_directInputController == null) return;

                _directInputController?.Unacquire();
                _directInputController?.Dispose();
                _directInputController = null;
                _playStationControllerGuid = Guid.Empty;

                // Notify developer
                ErrorLogger?.Invoke(null, "DirectInput controller disconnected."); // Log disconnection
            }
        }
        catch (Exception ex)
        {
            ErrorLogger?.Invoke(ex,
                $"Error during controller reconnection. The service will continue to retry.\n\n" +
                $"Exception type: {ex.GetType().Name}\n" +
                $"Exception details: {ex.Message}");

            // Clean up potentially invalid state to ensure a clean slate for the next attempt.
            _directInputController?.Unacquire();
            _directInputController?.Dispose();
            _directInputController = null;
            _playStationControllerGuid = Guid.Empty;

            // DO NOT call Stop(). Let the timer continue to run so we can try reconnecting again.
        }
    }

    private void HandleXInputRightButton(State state)
    {
        var isBDown = state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
        switch (isBDown)
        {
            case true when !_wasBDown:
                _mouseSimulator.RightButtonDown();
                break;
            case false when _wasBDown:
                _mouseSimulator.RightButtonUp();
                break;
        }

        _wasBDown = isBDown;
    }

    private void HandleXInputLeftButton(State state)
    {
        var isADown = state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
        switch (isADown)
        {
            case true when !_wasADown:
                _mouseSimulator.LeftButtonDown();
                break;
            case false when _wasADown:
                _mouseSimulator.LeftButtonUp();
                break;
        }

        _wasADown = isADown;
    }

    private void HandleXInputMovement(State state)
    {
        var (x, y) = ProcessThumbStickXInput(state.Gamepad.LeftThumbX, state.Gamepad.LeftThumbY, DeadZoneX, DeadZoneY);
        _mouseSimulator.MoveMouseBy((int)x, -(int)y);
    }

    private void HandleXInputScroll(State state)
    {
        var (x, y) =
            ProcessThumbStickXInput(state.Gamepad.RightThumbX, state.Gamepad.RightThumbY, DeadZoneX, DeadZoneY);
        _mouseSimulator.HorizontalScroll((int)x);
        _mouseSimulator.VerticalScroll((int)y);
    }

    private static (float, float) ProcessThumbStickXInput(short thumbX, short thumbY, float dzX, float dzY)
    {
        var normalizedX = Math.Max(-1, thumbX / MaxThumbValue);
        var normalizedY = Math.Max(-1, thumbY / MaxThumbValue);

        var resultX = Math.Abs(normalizedX) < dzX ? 0 : (Math.Abs(normalizedX) - dzX) * Math.Sign(normalizedX);
        var resultY = Math.Abs(normalizedY) < dzY ? 0 : (Math.Abs(normalizedY) - dzY) * Math.Sign(normalizedY);

        // Always apply base scaling, then additional scaling based on the deadzone
        resultX *= XInputScalingFactor;
        resultY *= XInputScalingFactor;

        if (dzX > 0) resultX *= 1.0f / (1.0f - dzX); // Scale up to full range

        if (dzY > 0) resultY *= 1.0f / (1.0f - dzY); // Scale up to full range

        return (resultX, resultY);
    }

    private void HandleDirectInputButtons(JoystickState state)
    {
        // DirectInput button indices can vary. Assuming common layout where 1 is Cross and 2 is Circle.
        const int crossButtonIndex = 1; // Assuming button 1 is Cross
        const int circleButtonIndex = 2; // Assuming button 2 is Circle

        if (state.Buttons.Length > Math.Max(crossButtonIndex, circleButtonIndex))
        {
            var isCrossDown = state.Buttons[crossButtonIndex];
            switch (isCrossDown)
            {
                case true when !_wasCrossDown:
                    _mouseSimulator.LeftButtonDown(); // Cross Button
                    break;
                case false when _wasCrossDown:
                    _mouseSimulator.LeftButtonUp();
                    break;
            }

            _wasCrossDown = isCrossDown;

            var isCircleDown = state.Buttons[circleButtonIndex];
            switch (isCircleDown)
            {
                case true when !_wasCircleDown:
                    _mouseSimulator.RightButtonDown(); // Circle Button
                    break;
                case false when _wasCircleDown:
                    _mouseSimulator.RightButtonUp();
                    break;
            }

            _wasCircleDown = isCircleDown;
        }
        else
        {
            // Notify developer
            // Log a warning or handle controllers with fewer buttons if necessary
            ErrorLogger?.Invoke(null,
                $"DirectInput controller has fewer than {Math.Max(crossButtonIndex, circleButtonIndex) + 1} buttons.");
        }
    }

    private void HandleDirectInputMovement(JoystickState state)
    {
        // Normalize DirectInput values from [0, 65535] to [-32767, 32768]
        if (state.X == int.MinValue || state.Y == int.MinValue) return;

        // Use int to prevent overflow when state.X/Y is 65535
        var thumbX = state.X - 32767;
        var thumbY = state.Y - 32767;

        // Invert the axes
        thumbY = -thumbY;
        thumbX = -thumbX;

        // Process the thumbstick values (Method signature updated below)
        var (x, y) = ProcessLeftThumbStickDirectInput(thumbX, thumbY, DeadZoneX, DeadZoneY);

        _mouseSimulator.MoveMouseBy(-(int)x, -(int)y);
    }

    private void HandleDirectInputScroll(JoystickState state)
    {
        // Use int to prevent overflow
        var thumbX = state.RotationZ - 32767;
        var thumbY = -(state.RotationZ - 32767);

        var (x, y) = ProcessRightThumbStickDirectInput(thumbX, thumbY, DeadZoneX, DeadZoneY);

        _mouseSimulator.HorizontalScroll((int)x);
        _mouseSimulator.VerticalScroll((int)y);
    }

    private static (float, float) ProcessLeftThumbStickDirectInput(int thumbX, int thumbY, float dzX, float dzY)
    {
        var normalizedX = thumbX / MaxThumbValue;
        var normalizedY = thumbY / MaxThumbValue;

        float resultX = 0;
        if (Math.Abs(normalizedX) > dzX)
            resultX = (Math.Abs(normalizedX) - dzX) * (normalizedX / Math.Abs(normalizedX));

        float resultY = 0;
        if (Math.Abs(normalizedY) > dzY)
            resultY = (Math.Abs(normalizedY) - dzY) * (normalizedY / Math.Abs(normalizedY));

        // Always apply base scaling, then additional scaling based on the deadzone
        resultX *= DirectInputLeftThumbStickScalingFactor;
        resultY *= DirectInputLeftThumbStickScalingFactor;

        if (dzX > 0) resultX *= 1.0f / (1.0f - dzX); // Scale up to full range

        if (dzY > 0) resultY *= 1.0f / (1.0f - dzY); // Scale up to full range

        return (resultX, resultY);
    }

    private static (float, float) ProcessRightThumbStickDirectInput(int thumbX, int thumbY, float dzX, float dzY)
    {
        var normalizedX = thumbX / MaxThumbValue;
        var normalizedY = thumbY / MaxThumbValue;

        // Apply the dead zone for X
        float resultX = 0;
        if (Math.Abs(normalizedX) > dzX)
            resultX = (Math.Abs(normalizedX) - dzX) * (normalizedX / Math.Abs(normalizedX));

        // Apply the dead zone for Y
        float resultY = 0;
        if (Math.Abs(normalizedY) > dzY)
            resultY = (Math.Abs(normalizedY) - dzY) * (normalizedY / Math.Abs(normalizedY));

        // Scale the values after dead zone adjustment
        resultX *= DirectInputRightThumbStickScalingFactor;
        resultY *= DirectInputRightThumbStickScalingFactor;

        if (dzX > 0) resultX *= 1.0f / (1.0f - dzX); // Scale up to full range

        if (dzY > 0) resultY *= 1.0f / (1.0f - dzY); // Scale up to full range

        return (resultX, resultY);
    }
}