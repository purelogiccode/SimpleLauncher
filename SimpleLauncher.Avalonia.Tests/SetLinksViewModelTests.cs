using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the SetLinksWindow ViewModel (Phase 4.1 port).
/// </summary>
public class SetLinksViewModelTests
{
    private const string DefaultYouTube = "https://www.youtube.com/results?search_query=";
    private const string DefaultIgdb = "https://www.igdb.com/search?q=";

    private static SetLinksViewModel CreateVm(out Mock<IMessageBoxLibraryService> messageBox,
        out SettingsManagerService settings)
    {
        messageBox = TestDependencies.MessageBox();
        settings = TestDependencies.Settings(
            TestEnvironment.ConfigurationFromJson(
                """{"Urls": {"YouTubeSearch": "https://www.youtube.com/results?search_query=", "IgdbSearch": "https://www.igdb.com/search?q="}}"""),
            messageBox);
        var vm = new SetLinksViewModel(settings, new ConfigurationBuilder().Build(),
            messageBox.Object, TestDependencies.ResourceProvider().Object);
        return vm;
    }

    [Fact]
    public void Ctor_LoadsUrlsFromSettings()
    {
        var messageBox = TestDependencies.MessageBox();
        var settings = TestDependencies.Settings(messageBox: messageBox);
        settings.VideoUrl = "https://custom.example/videos";
        settings.InfoUrl = "https://custom.example/info";

        var vm = new SetLinksViewModel(settings, new ConfigurationBuilder().Build(),
            messageBox.Object, TestDependencies.ResourceProvider().Object);

        Assert.Equal("https://custom.example/videos", vm.VideoUrl);
        Assert.Equal("https://custom.example/info", vm.InfoUrl);

        // Embedded UI icons: avares addresses resolved by PathToImageConverter.
        Assert.Equal("avares://SimpleLauncher.Avalonia/images/video.png", vm.VideoIconPath);
        Assert.Equal("avares://SimpleLauncher.Avalonia/images/info.png", vm.InfoIconPath);
    }

    [Fact]
    public async Task Save_EmptyValues_AppliesDefaults()
    {
        var vm = CreateVm(out var messageBox, out var settings);
        vm.VideoUrl = "   ";
        vm.InfoUrl = "";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(DefaultYouTube, settings.VideoUrl);
        Assert.Equal(DefaultIgdb, settings.InfoUrl);
        messageBox.Verify(m => m.LinksSavedMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public async Task Save_PreservesEnteredValues_AndRaisesSaveCompleted()
    {
        var vm = CreateVm(out _, out var settings);
        vm.VideoUrl = "https://my.example/search?q=";
        vm.InfoUrl = "https://my.example/info?q=";
        var saved = false;
        vm.SaveCompleted += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("https://my.example/search?q=", settings.VideoUrl);
        Assert.Equal("https://my.example/info?q=", settings.InfoUrl);
        Assert.True(saved);
    }

    [Fact]
    public async Task Revert_ResetsToConfiguredDefaults_AndCloses()
    {
        var vm = CreateVm(out var messageBox, out var settings);
        settings.VideoUrl = "changed";
        settings.InfoUrl = "changed";
        vm.VideoUrl = "changed";
        vm.InfoUrl = "changed";
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        await vm.RevertCommand.ExecuteAsync(null);

        Assert.Equal(DefaultYouTube, vm.VideoUrl);
        Assert.Equal(DefaultIgdb, vm.InfoUrl);
        messageBox.Verify(m => m.LinksRevertedMessageBoxAsync(), Times.Once);
        Assert.True(closed);
    }

    [Fact]
    public void Cancel_RaisesCloseRequested()
    {
        var vm = CreateVm(out _, out _);
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.CancelCommand.Execute(null);

        Assert.True(closed);
    }
}