using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Services.GamePad;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.GamePad;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the SDL-backed in-app gamepad navigation (non-Windows): dead zone
///     processing, focus movement, activation, context menu, Escape fallback and scrolling.
/// </summary>
public class GamepadNavigationServiceTests
{
    [Fact]
    public void WithDeadZone_ZeroesInsideDeadZone_AndRescalesTheRest()
    {
        var state = new GamepadInputState(0.02f, -0.6f, 0.5f, 0f, false, false, false, false, false, false);

        var processed = state.WithDeadZone(0.05f, 0.1f);

        Assert.Equal(0f, processed.LeftX);
        Assert.Equal(-(0.6f - 0.1f) / 0.9f, processed.LeftY, 5);
        Assert.Equal((0.5f - 0.05f) / 0.95f, processed.RightX, 5);
    }

    [Fact]
    public void ProcessInput_WhenNothingIsFocused_FocusesAControl()
    {
        var (window, _, _) = CreateAttachedWindow(out var service, out var controller);
        using (controller)
        using (service)
        {
            HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(down: true)));

            var focused = HeadlessAvalonia.RunOnUiThread(() => window.FocusManager?.GetFocusedElement());
            Assert.NotNull(focused);

            HeadlessAvalonia.RunOnUiThread(window.Close);
        }
    }

    [Fact]
    public void ProcessInput_LeftStickUpAndDown_MovesFocusAcrossControls()
    {
        var (window, first, second) = CreateAttachedWindow(out var service, out var controller);
        using (controller)
        using (service)
        {
            HeadlessAvalonia.RunOnUiThread(() => first.Focus());

            HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(leftY: -1f)));
            Assert.Same(second, HeadlessAvalonia.RunOnUiThread(() => window.FocusManager?.GetFocusedElement()));

            HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(leftY: 1f)));
            Assert.Same(first, HeadlessAvalonia.RunOnUiThread(() => window.FocusManager?.GetFocusedElement()));

            HeadlessAvalonia.RunOnUiThread(window.Close);
        }
    }

    [Fact]
    public void ProcessInput_PrimaryButton_ActivatesTheFocusedControl_OnPressEdgeOnly()
    {
        var (window, first, _) = CreateAttachedWindow(out var service, out var controller);
        using (controller)
        using (service)
        {
            var clicks = 0;
            HeadlessAvalonia.RunOnUiThread(() =>
            {
                first.Click += (_, _) => clicks++;
                first.Focus();
            });

            HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(primary: true)));
            Assert.Equal(1, clicks);

            // Held button must not repeat the activation.
            HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(primary: true)));
            Assert.Equal(1, clicks);

            HeadlessAvalonia.RunOnUiThread(() =>
            {
                service.ProcessInput(State());
                service.ProcessInput(State(primary: true));
            });
            Assert.Equal(2, clicks);

            HeadlessAvalonia.RunOnUiThread(window.Close);
        }
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public void ProcessInput_SecondaryButton_OpensTheContextMenu_OnPressEdgeOnly()
    {
        HeadlessAvalonia.EnsureInitialized();
        using var controller = CreateController();
        using var service = new GamepadNavigationService(controller, Log.Logger);
        var menuRequests = 0;

        var window = HeadlessAvalonia.RunOnUiThread(() =>
        {
            var w = new Window { Width = 400, Height = 300, Content = new Button { Content = "First" } };
            w.Show();
            service.Attach(w, () =>
            {
                menuRequests++;
                return true;
            });
            return w;
        });

        HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(secondary: true)));
        HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(secondary: true)));
        Assert.Equal(1, menuRequests);

        HeadlessAvalonia.RunOnUiThread(window.Close);
    }

    [Fact]
    public void ProcessInput_SecondaryButton_WithoutMenu_FallsBackToEscape()
    {
        var (window, _, _) = CreateAttachedWindow(out var service, out var controller, openContextMenu: () => false);
        using (controller)
        using (service)
        {
            Key? captured = null;
            HeadlessAvalonia.RunOnUiThread(() => window.KeyDown += (_, e) => captured = e.Key);

            HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(secondary: true)));

            Assert.Equal(Key.Escape, captured);

            HeadlessAvalonia.RunOnUiThread(window.Close);
        }
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public void ProcessInput_RightStick_ScrollsTheFocusedScrollViewer()
    {
        HeadlessAvalonia.EnsureInitialized();
        using var controller = CreateController();
        using var service = new GamepadNavigationService(controller, Log.Logger);
        ScrollViewer? viewer = null;

        var window = HeadlessAvalonia.RunOnUiThread(() =>
        {
            if (Application.Current is { } app && !app.Styles.OfType<FluentTheme>().Any())
                app.Styles.Add(new FluentTheme());

            var panel = new StackPanel();
            for (var i = 0; i < 30; i++) panel.Children.Add(new Button { Content = $"Item {i}", Height = 40 });
            viewer = new ScrollViewer { Width = 400, Height = 300, Content = panel };
            var w = new Window { Width = 420, Height = 320, Content = viewer };
            w.Show();
            service.Attach(w, () => false);
            panel.Children[0].Focus();
            Dispatcher.UIThread.RunJobs();
            return w;
        });

        HeadlessAvalonia.RunOnUiThread(() => service.ProcessInput(State(rightY: -1f)));

        var offset = HeadlessAvalonia.RunOnUiThread(() => viewer?.Offset.Y ?? 0);
        Assert.True(offset > 0, $"Expected the scroll viewer to scroll down, but the offset was {offset}.");

        HeadlessAvalonia.RunOnUiThread(window.Close);
    }

    private static GamePadController CreateController()
    {
        return new GamePadController(
            new Mock<IMessageBoxLibraryService>().Object,
            new ConfigurationBuilder().Build(),
            Log.Logger);
    }

    private static (Window Window, Button First, Button Second) CreateAttachedWindow(
        out GamepadNavigationService service, out GamePadController controller,
        Func<bool>? openContextMenu = null)
    {
        HeadlessAvalonia.EnsureInitialized();
        controller = CreateController();
        service = new GamepadNavigationService(controller, Log.Logger);

        var navigationService = service;
        Button first = null!;
        Button second = null!;
        var window = HeadlessAvalonia.RunOnUiThread(() =>
        {
            first = new Button { Content = "First" };
            second = new Button { Content = "Second" };
            var w = new Window
            {
                Width = 400,
                Height = 300,
                Content = new StackPanel { Children = { first, second } },
            };
            w.Show();
            navigationService.Attach(w, openContextMenu ?? (() => false));
            return w;
        });

        return (window, first, second);
    }

    private static GamepadInputState State(
        float leftX = 0f, float leftY = 0f, float rightX = 0f, float rightY = 0f,
        bool up = false, bool down = false, bool left = false, bool right = false,
        bool primary = false, bool secondary = false)
    {
        return new GamepadInputState(leftX, leftY, rightX, rightY, up, down, left, right, primary, secondary);
    }
}
