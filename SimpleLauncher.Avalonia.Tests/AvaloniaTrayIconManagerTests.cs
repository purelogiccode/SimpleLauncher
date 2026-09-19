using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Moq;
using SimpleLauncher.Avalonia.Services.TrayIcon;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="AvaloniaTrayIconManager" /> (Phase 7 cross-platform service)
///     under the headless Avalonia platform. The headless windowing platform returns a
///     null ITrayIconImpl, so the tray icon is inert — but the menu structure, the
///     Application attachment, and the lifecycle contract are still fully observable.
/// </summary>
public class AvaloniaTrayIconManagerTests
{
    [Fact]
    public void Ctor_NullLifetime_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AvaloniaTrayIconManager(null!, new Mock<ILogger>().Object));
    }

    [Fact]
    public void Ctor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AvaloniaTrayIconManager(new Mock<IApplicationLifetime>().Object, null!));
    }

    [Fact]
    public void Initialize_NullWindow_Throws()
    {
        HeadlessAvalonia.EnsureInitialized();
        var manager = new AvaloniaTrayIconManager(new Mock<IApplicationLifetime>().Object, new Mock<ILogger>().Object);
        Assert.Throws<ArgumentNullException>(() => manager.Initialize(null!));
    }

    [Fact]
    public void Initialize_AttachesTrayIconWithExpectedMenu()
    {
        HeadlessAvalonia.EnsureInitialized();
        var manager = new AvaloniaTrayIconManager(new Mock<IApplicationLifetime>().Object, new Mock<ILogger>().Object);
        var window = HeadlessAvalonia.RunOnUiThread(() => new Window());

        try
        {
            HeadlessAvalonia.RunOnUiThread(() => manager.Initialize(window));

            HeadlessAvalonia.RunOnUiThread(() =>
            {
                var icons = TrayIcon.GetIcons(Application.Current!);
                Assert.NotNull(icons);
                var trayIcon = Assert.Single(icons);

                var menu = Assert.IsType<NativeMenu>(trayIcon.Menu);
                Assert.Equal(5, menu.Items.Count);
                Assert.Equal("Open", ((NativeMenuItem)menu.Items[0]).Header);
                Assert.Equal("Minimize to Tray", ((NativeMenuItem)menu.Items[1]).Header);
                Assert.IsType<NativeMenuItemSeparator>(menu.Items[2]);
                Assert.Equal("Debug Window", ((NativeMenuItem)menu.Items[3]).Header);
                Assert.Equal("Exit", ((NativeMenuItem)menu.Items[4]).Header);
            });
        }
        finally
        {
            // Dispose and close on the UI thread (the same thread the manager's
            // resources were created on).
            HeadlessAvalonia.RunOnUiThread(() =>
            {
                manager.Dispose();
                window.Close();
            });
        }
    }

    [Fact]
    public void Initialize_TrayIconClicked_ShowsAndActivatesWindow()
    {
        HeadlessAvalonia.EnsureInitialized();
        var manager = new AvaloniaTrayIconManager(new Mock<IApplicationLifetime>().Object, new Mock<ILogger>().Object);
        var window = HeadlessAvalonia.RunOnUiThread(() => new Window());

        try
        {
            HeadlessAvalonia.RunOnUiThread(() => manager.Initialize(window));
            HeadlessAvalonia.RunOnUiThread(() => window.Hide());

            RaiseClicked();

            HeadlessAvalonia.RunOnUiThread(() =>
            {
                Assert.True(window.IsVisible);
                Assert.Equal(WindowState.Normal, window.WindowState);
                Assert.True(window.ShowInTaskbar);
            });
        }
        finally
        {
            HeadlessAvalonia.RunOnUiThread(() =>
            {
                manager.Dispose();
                window.Close();
            });
        }
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        HeadlessAvalonia.EnsureInitialized();
        var manager = new AvaloniaTrayIconManager(new Mock<IApplicationLifetime>().Object, new Mock<ILogger>().Object);
        var window = HeadlessAvalonia.RunOnUiThread(() => new Window());

        try
        {
            HeadlessAvalonia.RunOnUiThread(() => manager.Initialize(window));
            HeadlessAvalonia.RunOnUiThread(() =>
            {
                manager.Dispose();
                manager.Dispose(); // must not throw
            });
        }
        finally
        {
            HeadlessAvalonia.RunOnUiThread(window.Close);
        }
    }

    [Fact]
    public void Initialize_AfterDispose_IsNoOp()
    {
        HeadlessAvalonia.EnsureInitialized();
        var manager = new AvaloniaTrayIconManager(new Mock<IApplicationLifetime>().Object, new Mock<ILogger>().Object);
        var window = HeadlessAvalonia.RunOnUiThread(() => new Window());

        try
        {
            HeadlessAvalonia.RunOnUiThread(() => manager.Initialize(window));
            HeadlessAvalonia.RunOnUiThread(() =>
            {
                manager.Dispose();
                manager.Initialize(window); // no-op, must not throw
            });

            // The disposed manager never created a second icon and never detached the
            // first one (detachment is the app-lifetime's job) — exactly one icon remains.
            HeadlessAvalonia.RunOnUiThread(() =>
            {
                var icons = TrayIcon.GetIcons(Application.Current!);
                Assert.NotNull(icons);
                Assert.Single(icons);
            });
        }
        finally
        {
            HeadlessAvalonia.RunOnUiThread(window.Close);
        }
    }

    [Fact]
    public void TrayIconAsset_IsEmbeddedInAssembly()
    {
        // Regression guard: the tray icon must ship inside the assembly (avares), not
        // as a loose file — a missing asset leaves the tray icon imageless.
        HeadlessAvalonia.EnsureInitialized();

        var uri = new Uri("avares://SimpleLauncher.Avalonia/icon/icon.ico");
        Assert.True(AssetLoader.Exists(uri));
        using var stream = AssetLoader.Open(uri);
        Assert.True(stream.Length > 0);
    }

    /// <summary>
    ///     Raises the TrayIcon Clicked event via reflection (events can only be invoked
    ///     from within their declaring type).
    /// </summary>
    private static void RaiseClicked()
    {
        HeadlessAvalonia.RunOnUiThread(() =>
        {
            var trayIcon = Assert.Single(TrayIcon.GetIcons(Application.Current!)!);
            var field = typeof(TrayIcon).GetField("Clicked", BindingFlags.NonPublic | BindingFlags.Instance);
            var handler = (EventHandler?)field?.GetValue(trayIcon);
            handler?.Invoke(trayIcon, EventArgs.Empty);
        });
    }
}