using Moq;
using SimpleLauncher.Avalonia.Services.GameFilter;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the game-list filters: Show Games cover detection and the WPF-parity
///     search matcher (file name, MAME description, multi-term AND/OR and quoted phrases).
/// </summary>
public class AvaloniaGameFilterServiceTests
{
    private static AvaloniaGameFilterService CreateService(string showGames)
    {
        var settings = TestDependencies.Settings();
        settings.ShowGames = showGames;
        return new AvaloniaGameFilterService(
            settings,
            new Mock<IMameDataService>().Object);
    }

    private static List<GameCardViewModel> CreateGames(string realCoverPath)
    {
        return
        [
            new GameCardViewModel { FileName = "WithCover", CoverPath = realCoverPath },
            new GameCardViewModel { FileName = "DefaultCover", CoverPath = "/tmp/images/default.png" },
            new GameCardViewModel { FileName = "MissingCover", CoverPath = "" }
        ];
    }

    private static List<GameCardViewModel> CreateSearchGames()
    {
        return
        [
            new GameCardViewModel { FileName = "abyss", DisplayTitle = "abyss" },
            new GameCardViewModel { FileName = "beyond", DisplayTitle = "", MachineDescription = "Beyond the Beyond" },
            new GameCardViewModel { FileName = "cantina", DisplayTitle = "Cantina", MachineDescription = "Cantina" }
        ];
    }

    [Fact]
    public void FilterBySearchQuery_MatchesFileNameAndMameDescription_WhenDisplayTitleIsEmpty()
    {
        var games = CreateSearchGames();

        Assert.Equal(["beyond"],
            AvaloniaGameFilterService.FilterBySearchQuery(games, "Beyond the Beyond").Select(g => g.FileName).ToArray());
        Assert.Equal(["beyond"],
            AvaloniaGameFilterService.FilterBySearchQuery(games, "beyond").Select(g => g.FileName).ToArray());
    }

    [Fact]
    public void FilterBySearchQuery_SplitsMultiWordQueriesLikeWpf()
    {
        var games = new List<GameCardViewModel>
        {
            new() { FileName = "super_mario_bros", DisplayTitle = "super_mario_bros" },
            new() { FileName = "sf2", DisplayTitle = "sf2", MachineDescription = "Street Fighter II" }
        };

        // The old Avalonia filter used one Contains(query); spaces in the query therefore
        // never matched underscored file names. WPF splits terms and ANDs them.
        Assert.Single(AvaloniaGameFilterService.FilterBySearchQuery(games, "super mario"));
        Assert.Single(AvaloniaGameFilterService.FilterBySearchQuery(games, "street fighter"));
    }

    [Fact]
    public void FilterBySearchQuery_SupportsAndOrAndQuotedPhrases()
    {
        var games = CreateSearchGames();

        Assert.Empty(AvaloniaGameFilterService.FilterBySearchQuery(games, "beyond abyss"));

        Assert.Equal(["abyss", "cantina"],
            AvaloniaGameFilterService.FilterBySearchQuery(games, "abyss OR cantina").Select(g => g.FileName).ToArray());

        Assert.Equal(["beyond"],
            AvaloniaGameFilterService.FilterBySearchQuery(games, "\"Beyond the Beyond\"").Select(g => g.FileName).ToArray());
    }

    [Fact]
    public void FilterByShowGamesSetting_ShowWithoutCover_ReturnsOnlyGamesWithoutARealCover()
    {
        var realCover = Path.Combine(Path.GetTempPath(), $"sl-cover-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(realCover, [1, 2, 3]);
        try
        {
            var service = CreateService("ShowWithoutCover");

            var filtered = service.FilterByShowGamesSetting(CreateGames(realCover));

            Assert.Equal(["DefaultCover", "MissingCover"], filtered.Select(g => g.FileName).ToArray());
        }
        finally
        {
            File.Delete(realCover);
        }
    }

    [Fact]
    public void FilterByShowGamesSetting_ShowWithCover_ReturnsOnlyGamesWithARealCover()
    {
        var realCover = Path.Combine(Path.GetTempPath(), $"sl-cover-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(realCover, [1, 2, 3]);
        try
        {
            var service = CreateService("ShowWithCover");

            var filtered = service.FilterByShowGamesSetting(CreateGames(realCover));

            Assert.Equal(["WithCover"], filtered.Select(g => g.FileName).ToArray());
        }
        finally
        {
            File.Delete(realCover);
        }
    }

    [Fact]
    public void FilterByShowGamesSetting_ShowAll_ReturnsEverythingUnchanged()
    {
        var service = CreateService("ShowAll");

        var games = CreateGames("/tmp/images/real.png");
        var filtered = service.FilterByShowGamesSetting(games);

        Assert.Same(games, filtered);
    }
}
