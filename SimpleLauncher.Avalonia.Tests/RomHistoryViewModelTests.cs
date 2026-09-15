using Moq;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using CoreMessageBoxResult = SimpleLauncher.Core.Models.MessageBoxResult;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the RomHistoryWindow ViewModel (Phase 4.1 port).
/// </summary>
public class RomHistoryViewModelTests
{
    private const string HistoryXml = """
                                      <history>
                                        <entry>
                                          <systems><system name="Super Mario Bros (World)" game="Super Mario Bros (World)" /></systems>
                                          <text>The classic platformer released in 1985.</text>
                                        </entry>
                                      </history>
                                      """;

    private static RomHistoryViewModel CreateVm(out Mock<IMessageBoxLibraryService> messageBox)
    {
        messageBox = TestDependencies.MessageBox();
        return new RomHistoryViewModel(TestDependencies.Logger().Object, messageBox.Object,
            TestDependencies.ResourceProvider().Object);
    }

    [Fact]
    public void Initialize_SetsRomTexts()
    {
        var vm = CreateVm(out _);
        vm.Initialize("Super Mario Bros (World).nes", "NES", "Super Mario Bros");

        Assert.Equal("Super Mario Bros (World).nes", vm.RomNameText);
        Assert.Equal("Super Mario Bros", vm.RomDescriptionText);
        Assert.False(vm.IsDescriptionVisible);
    }

    [Fact]
    public async Task LoadHistory_NoDatabaseFiles_ShowsFallbackAndPrompts()
    {
        var historyDat = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.dat");
        var historyXml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.xml");
        File.Delete(historyDat);
        File.Delete(historyXml);

        var vm = CreateVm(out var messageBox);
        vm.Initialize("game.nes", "NES", "game");

        await vm.LoadRomHistoryAsync();

        Assert.Contains("history.dat", vm.HistoryText, StringComparison.OrdinalIgnoreCase);
        messageBox.Verify(m => m.NoHistoryXmlOrDatFoundMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public async Task LoadHistory_EntryFound_ShowsHistoryText()
    {
        var historyDat = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.dat");
        var historyXml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.xml");
        File.Delete(historyDat);
        File.WriteAllText(historyXml, HistoryXml);
        try
        {
            var vm = CreateVm(out var messageBox);
            vm.Initialize("Super Mario Bros (World)", "NES", "Super Mario Bros");

            await vm.LoadRomHistoryAsync();

            Assert.True(vm.IsDescriptionVisible);
            Assert.Contains("The classic platformer released in 1985.", vm.HistoryText,
                StringComparison.OrdinalIgnoreCase);
            messageBox.Verify(m => m.NoHistoryXmlOrDatFoundMessageBoxAsync(), Times.Never);
            messageBox.Verify(m => m.SearchOnlineForRomHistoryMessageBoxAsync(), Times.Never);
        }
        finally
        {
            File.Delete(historyXml);
        }
    }

    [Fact]
    public async Task LoadHistory_EntryNotFound_PromptsForOnlineSearch()
    {
        var historyDat = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.dat");
        var historyXml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.xml");
        File.Delete(historyDat);
        File.WriteAllText(historyXml, HistoryXml);
        try
        {
            var vm = CreateVm(out var messageBox);
            messageBox.Setup(m => m.SearchOnlineForRomHistoryMessageBoxAsync()).ReturnsAsync(CoreMessageBoxResult.No);
            vm.Initialize("Unknown Game (World).nes", "NES", "Unknown Game");

            await vm.LoadRomHistoryAsync();

            messageBox.Verify(m => m.SearchOnlineForRomHistoryMessageBoxAsync(), Times.Once);
            Assert.Contains("local database", vm.HistoryText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(historyXml);
        }
    }

    [Fact]
    public async Task LoadHistory_CorruptXml_PromptsForOnlineSearch()
    {
        var historyDat = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.dat");
        var historyXml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.xml");
        File.Delete(historyDat);
        File.WriteAllText(historyXml, "<not-valid-xml");
        try
        {
            var vm = CreateVm(out var messageBox);
            messageBox.Setup(m => m.SearchOnlineForRomHistoryMessageBoxAsync()).ReturnsAsync(CoreMessageBoxResult.No);
            vm.Initialize("game.nes", "NES", "game");

            await vm.LoadRomHistoryAsync();

            // A corrupt database is treated as "no entry" and falls back gracefully
            // instead of throwing to the UI (CORE-29).
            messageBox.Verify(m => m.ErrorLoadingRomHistoryMessageBoxAsync(), Times.Never);
            messageBox.Verify(m => m.SearchOnlineForRomHistoryMessageBoxAsync(), Times.Once);
        }
        finally
        {
            File.Delete(historyXml);
        }
    }
}