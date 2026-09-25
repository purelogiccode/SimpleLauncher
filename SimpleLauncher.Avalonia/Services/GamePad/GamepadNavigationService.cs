using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
// ReSharper disable once RedundantUsingDirective
using Avalonia.Threading;
using Avalonia.VisualTree;
using SimpleLauncher.Core.Services.GamePad;

namespace SimpleLauncher.Avalonia.Services.GamePad;

/// <summary>
///     Translates controller input reported by <see cref="GamePadController" /> into in-app
///     navigation on non-Windows platforms: the left stick / D-pad move keyboard focus, the
///     A button activates the focused control, B opens the selected game's context menu (or
///     falls back to Escape, which closes dialogs) and the right stick scrolls the focused
///     scroll viewer. The Windows apps keep the WPF-compatible OS-level mouse simulation and
///     never raise the controller's input event, so this service stays inert there.
/// </summary>
public sealed class GamepadNavigationService : IDisposable
{
    private const float DirectionThreshold = 0.35f;
    private const long RepeatDelayMilliseconds = 350;
    private const long RepeatIntervalMilliseconds = 130;
    private const long ScrollRepeatMilliseconds = 80;
    private const float ScrollPixelsPerTick = 45f;

    private readonly GamePadController _controller;
    private readonly ILogger _logger;

    private Func<bool>? _openContextMenu;
    private TopLevel? _target;
    private NavDirection _lastDirection = NavDirection.None;
    private long _nextRepeatAt;
    private long _nextScrollAt;
    private bool _wasPrimaryPressed;
    private bool _wasSecondaryPressed;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GamepadNavigationService" /> class and
    ///     starts listening for controller state snapshots.
    /// </summary>
    /// <param name="controller">The controller whose input is translated into navigation.</param>
    /// <param name="logger">The logger instance.</param>
    public GamepadNavigationService(GamePadController controller, ILogger logger)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
#if !WINDOWS
        _controller.InputChanged += OnInputChanged;
#endif
    }

    /// <summary>
    ///     Attaches the window that receives gamepad navigation and the callback that opens
    ///     the context menu of the current selection.
    /// </summary>
    public void Attach(TopLevel target, Func<bool> openContextMenu)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _openContextMenu = openContextMenu ?? throw new ArgumentNullException(nameof(openContextMenu));
        ResetInputState();
        _logger.Debug("Gamepad navigation attached to the main window");
    }

    /// <summary>
    ///     Detaches the window when it closes.
    /// </summary>
    public void Detach(TopLevel target)
    {
        if (!ReferenceEquals(_target, target)) return;

        _target = null;
        _openContextMenu = null;
        ResetInputState();
        _logger.Debug("Gamepad navigation detached from the main window");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
#if !WINDOWS
        _controller.InputChanged -= OnInputChanged;
#endif
    }

#if !WINDOWS
    private void OnInputChanged(GamepadInputState state)
    {
        if (_disposed) return;

        // Raised on the SDL polling thread; the actual input dispatch happens on the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) ProcessInput(state);
        }, DispatcherPriority.Input);
    }
#endif

    /// <summary>
    ///     Applies one controller snapshot to the attached window. Internal so tests can drive
    ///     the dispatch logic without SDL; must be called on the UI thread.
    /// </summary>
    internal void ProcessInput(GamepadInputState state)
    {
        var target = _target;
        if (target is null) return;

        var now = Environment.TickCount64;
        ProcessMovement(target, state, now);
        ProcessButtons(target, state);
        ProcessScroll(target, state, now);
    }

    private void ProcessMovement(TopLevel target, GamepadInputState state, long now)
    {
        var direction = ResolveDirection(state);
        if (direction == NavDirection.None)
        {
            _lastDirection = NavDirection.None;
            return;
        }

        if (direction != _lastDirection)
        {
            _lastDirection = direction;
            _nextRepeatAt = now + RepeatDelayMilliseconds;
            MoveFocus(target, direction);
            _logger.Debug("[Gamepad] Focus move: {Direction}", direction);
            return;
        }

        if (now < _nextRepeatAt) return;

        _nextRepeatAt = now + RepeatIntervalMilliseconds;
        MoveFocus(target, direction);
        _logger.Debug("[Gamepad] Focus move: {Direction}", direction);
    }

    private static NavDirection ResolveDirection(GamepadInputState state)
    {
        if (state.DpadUp) return NavDirection.Up;
        if (state.DpadDown) return NavDirection.Down;
        if (state.DpadLeft) return NavDirection.Left;
        if (state.DpadRight) return NavDirection.Right;

        var horizontal = state.LeftX;
        var vertical = state.LeftY;
        if (Math.Abs(horizontal) < DirectionThreshold && Math.Abs(vertical) < DirectionThreshold)
            return NavDirection.None;

        if (Math.Abs(horizontal) >= Math.Abs(vertical))
            return horizontal > 0 ? NavDirection.Right : NavDirection.Left;

        return vertical > 0 ? NavDirection.Up : NavDirection.Down;
    }

    private static void MoveFocus(TopLevel target, NavDirection direction)
    {
        if (target.FocusManager?.GetFocusedElement() is not InputElement focused)
        {
            // Nothing is focused yet (for example right after startup): let the tab order
            // pick the first focusable control.
            RaiseKey(target, Key.Tab);
            return;
        }

        var key = direction switch
        {
            NavDirection.Up => Key.Up,
            NavDirection.Down => Key.Down,
            NavDirection.Left => Key.Left,
            _ => Key.Right,
        };

        var args = CreateKeyArgs(key);
        focused.RaiseEvent(args);
        if (args.Handled) return;

        // Neither the focused control nor the window's directional navigation consumed the
        // key (focus is at an edge): fall back to the tab order so the user cannot get stuck.
        var backwards = direction is NavDirection.Up or NavDirection.Left;
        RaiseKey(focused, Key.Tab, backwards ? KeyModifiers.Shift : KeyModifiers.None);
    }

    private void ProcessButtons(TopLevel target, GamepadInputState state)
    {
        if (state.PrimaryPressed && !_wasPrimaryPressed)
        {
            ActivateFocused(target);
            _logger.Debug("[Gamepad] Primary action (activate)");
        }

        _wasPrimaryPressed = state.PrimaryPressed;

        if (state.SecondaryPressed && !_wasSecondaryPressed)
        {
            GoBack(target);
            _logger.Debug("[Gamepad] Secondary action (context menu or back)");
        }

        _wasSecondaryPressed = state.SecondaryPressed;
    }

    private static void ActivateFocused(TopLevel target)
    {
        var focused = target.FocusManager?.GetFocusedElement() as InputElement ?? target;
        RaiseKey(focused, Key.Enter);
    }

    private void GoBack(TopLevel target)
    {
        // B mirrors the Windows right-click: open the context menu of the selected game.
        // When nothing opens (for example a dialog owns the focus), fall back to Escape.
        if (_openContextMenu?.Invoke() == true) return;

        var focused = target.FocusManager?.GetFocusedElement() as InputElement ?? target;
        RaiseKey(focused, Key.Escape);
    }

    private void ProcessScroll(TopLevel target, GamepadInputState state, long now)
    {
        if (Math.Abs(state.RightY) < DirectionThreshold)
        {
            _nextScrollAt = 0;
            return;
        }

        if (now < _nextScrollAt) return;
        _nextScrollAt = now + ScrollRepeatMilliseconds;

        var focused = target.FocusManager?.GetFocusedElement() as Visual;
        var viewer = focused?.FindAncestorOfType<ScrollViewer>(includeSelf: true)
                     ?? target.FindDescendantOfType<ScrollViewer>();
        if (viewer is null) return;

        var delta = -state.RightY * ScrollPixelsPerTick;
        var maxOffset = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
        var newOffset = Math.Clamp(viewer.Offset.Y + delta, 0, maxOffset);
        viewer.Offset = new Vector(viewer.Offset.X, newOffset);
    }

    private void ResetInputState()
    {
        _lastDirection = NavDirection.None;
        _nextRepeatAt = 0;
        _nextScrollAt = 0;
        _wasPrimaryPressed = false;
        _wasSecondaryPressed = false;
    }

    private static KeyEventArgs CreateKeyArgs(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        return new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            KeyDeviceType = KeyDeviceType.Gamepad,
        };
    }

    private static void RaiseKey(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        target.RaiseEvent(CreateKeyArgs(key, modifiers));
    }

    private enum NavDirection
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }
}
