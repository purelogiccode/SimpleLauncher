using System.Net;
using System.Reflection;
using Avalonia.Controls;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.RetroAchievements;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.RetroAchievements;
using IResourceProvider = SimpleLauncher.Core.Interfaces.IResourceProvider;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression test for the RetroAchievements window with no credentials configured
///     (WPF parity): the initial profile load is triggered from Loaded, and the
///     credentials-missing path completes synchronously, so the inline NoProfile panel
///     must become visible and the loading overlay must be hidden instead of staying
///     stuck on "Loading...".
/// </summary>
[Collection(nameof(MutatesStaticAppState))]
public class RetroAchievementsWindowNoCredentialsTests
{
    [Fact]
    public async Task WithoutCredentials_ShowsNoProfilePanelAndHidesLoadingOverlay()
    {
        HeadlessAvalonia.EnsureInitialized();

        var resourceProvider = TestDependencies.ResourceProvider();
        var messageBox = TestDependencies.MessageBox();
        var logger = TestDependencies.Logger();
        var settings = TestDependencies.Settings(messageBox: messageBox);
        settings.RaUsername = "";
        settings.RaApiKey = "";

        InstallFakeAppServiceProvider(resourceProvider.Object, messageBox.Object);

        var raService = new RetroAchievementsService(
            TestDependencies.HttpFactory(
                TestDependencies.HttpClientWith(_ => new HttpResponseMessage(HttpStatusCode.OK))).Object,
            new RetroAchievementsManager(),
            new ConfigurationBuilder().Build(),
            logger.Object);

        RetroAchievementsWindow? window = null;
        Border? loadingOverlay = null;
        Grid? noProfileOverlay = null;
        TextBlock? mainMessage = null;
        TextBlock? subMessage = null;

        HeadlessAvalonia.RunOnUiThread(() =>
        {
            window = new RetroAchievementsWindow(
                TestDependencies.PlaySound(settings), logger.Object, settings, raService);
            loadingOverlay = window.FindControl<Border>("LoadingOverlay");
            noProfileOverlay = window.FindControl<Grid>("NoProfileOverlay");
            mainMessage = window.FindControl<TextBlock>("NoProfileMainMessage");
            subMessage = window.FindControl<TextBlock>("NoProfileSubMessage");
            window.Show();
        });

        try
        {
            Assert.NotNull(window);
            Assert.NotNull(loadingOverlay);
            Assert.NotNull(noProfileOverlay);
            Assert.NotNull(mainMessage);
            Assert.NotNull(subMessage);

            await HeadlessAvalonia.WaitUntilAsync(() => HeadlessAvalonia.RunOnUiThread(() => window!.IsLoaded));
            await HeadlessAvalonia.WaitUntilAsync(() =>
                HeadlessAvalonia.RunOnUiThread(() => noProfileOverlay!.IsVisible && !loadingOverlay!.IsVisible));

            HeadlessAvalonia.RunOnUiThread(() =>
            {
                Assert.False(loadingOverlay!.IsVisible);
                Assert.True(noProfileOverlay!.IsVisible);
                Assert.Contains("not set", mainMessage!.Text ?? "", StringComparison.OrdinalIgnoreCase);
                Assert.Contains("configure your credentials", subMessage!.Text ?? "",
                    StringComparison.OrdinalIgnoreCase);
            });
        }
        finally
        {
            if (window is not null) HeadlessAvalonia.RunOnUiThread(() => window.Close());
        }
    }

    private static void InstallFakeAppServiceProvider(IResourceProvider resourceProvider,
        IMessageBoxLibraryService messageBox)
    {
        var prop = typeof(App).GetProperty("ServiceProvider", BindingFlags.Static | BindingFlags.Public);
        prop!.GetSetMethod(true)!.Invoke(null, [new FakeWindowServiceProvider(resourceProvider, messageBox)]);
    }

    private sealed class FakeWindowServiceProvider(
        IResourceProvider resourceProvider,
        IMessageBoxLibraryService messageBox) : IServiceProvider
    {
        private readonly IResourceProvider _resourceProvider = resourceProvider;
        private readonly IMessageBoxLibraryService _messageBox = messageBox;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IResourceProvider)) return _resourceProvider;
            if (serviceType == typeof(IMessageBoxLibraryService)) return _messageBox;
            if (serviceType == typeof(LocalizationService)) return new LocalizationService();
            return null;
        }
    }
}