using Moq;
using SimpleLauncher.Avalonia.Services.GameFilter;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the Show Games filter: the cover resolver falls back to images\default.png,
///     so "has a cover" must mean "has a real cover", not "the resolved path exists".
/// </summary>
public class AvaloniaGameFilterServiceTests
{
    private static AvaloniaGameFilterService CreateService(string showGames)
    {
        var settings = TestDependencies.Settings();
        settings.ShowGames = showGames;
        return new AvaloniaGameFilterService(
            new Mock<IFindCoverImageService>().Object,
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
