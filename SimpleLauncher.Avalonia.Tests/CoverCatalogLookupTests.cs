using SimpleLauncher.Core.Services.CoverCatalog;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the cover catalog matcher used by both the R2 client and the local
///     "already present" check.
/// </summary>
public class CoverCatalogLookupTests
{
    private static CoverCatalogLookup CreateLookup()
    {
        return new CoverCatalogLookup(
        [
            new CoverCatalogEntry
            {
                File = "1942.jpg",
                Names = ["1942", "1942 (1986)(Elite Systems)", "1942 (1986)(Elite Systems)[t]"]
            },
            new CoverCatalogEntry
            {
                File = "Super Mario Bros.jpg",
                Names = ["Super Mario Bros", "Super Mario Brothers"]
            },
            new CoverCatalogEntry
            {
                File = "Legend of Zelda, The.jpg",
                Names = ["Legend of Zelda, The", "The Legend of Zelda"]
            }
        ]);
    }

    [Fact]
    public void FindBestMatch_ExactAliasWins()
    {
        var entry = CreateLookup().FindBestMatch("1942 (1986)(Elite Systems)", 0);

        Assert.NotNull(entry);
        Assert.Equal("1942.jpg", entry.File);
    }

    [Fact]
    public void FindBestMatch_AnnotationStrippingMatches()
    {
        var entry = CreateLookup().FindBestMatch("1942 (USA) (Rev 1)", 0);

        Assert.NotNull(entry);
        Assert.Equal("1942.jpg", entry.File);
    }

    [Fact]
    public void FindBestMatch_FuzzyTypoMatchesAtDefaultThreshold()
    {
        var entry = CreateLookup().FindBestMatch("Super Mario Bro.s (USA)", 0.8);

        Assert.NotNull(entry);
        Assert.Equal("Super Mario Bros.jpg", entry.File);
    }

    [Fact]
    public void FindBestMatch_ArticleAndWordOrderMatch()
    {
        var entry = CreateLookup().FindBestMatch("The Legend of Zelda (USA)", 0);

        Assert.NotNull(entry);
        Assert.Equal("Legend of Zelda, The.jpg", entry.File);
    }

    [Fact]
    public void FindBestMatch_DifferentGameDoesNotMatch()
    {
        var entry = CreateLookup().FindBestMatch("1943 - The Battle of Midway", 0.8);

        Assert.Null(entry);
    }

    [Fact]
    public void FindBestMatch_EmptyInputReturnsNull()
    {
        Assert.Null(CreateLookup().FindBestMatch("", 0.8));
        Assert.Null(CreateLookup().FindBestMatch(null, 0.8));
    }

    [Fact]
    public void FindBestMatch_WithoutAnnotationStrippingDoesNotMatchVariant()
    {
        var lookup = new CoverCatalogLookup(
        [
            new CoverCatalogEntry { File = "1942.jpg", Names = ["1942"] }
        ], stripAnnotations: false);

        Assert.Null(lookup.FindBestMatch("1942 (USA)", 0));
        Assert.NotNull(lookup.FindBestMatch("1942", 0));
    }
}
