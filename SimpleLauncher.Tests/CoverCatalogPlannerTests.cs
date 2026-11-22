using SimpleLauncher.Core.Services.CoverCatalog;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the shared cover-catalog normalization and grouping rules. The same
///     primitives are used by the R2 catalog builder, so these tests keep the upload set
///     stable and prevent distinct games from being merged.
/// </summary>
public class CoverCatalogPlannerTests
{
    /// <summary>Annotation variants collapse into one canonical group with aliases.</summary>
    [Fact]
    public void GroupByName_MergesAnnotationVariantsAndKeepsAliases()
    {
        var groups = CoverCatalogPlanner.GroupByName(
        [
            new CoverCatalogCandidate("1942", ".png", 100, 320, 600),
            new CoverCatalogCandidate("1942 (1986)(Elite Systems)", ".png", 50, 320, 600),
            new CoverCatalogCandidate("1942 (1986)(Elite Systems)[t]", ".png", 25, 320, 600)
        ]);

        var group = Assert.Single(groups);
        Assert.Equal("1942", group.Canonical.BaseName);
        Assert.Equal(3, group.Aliases.Count);
        Assert.Contains("1942 (1986)(Elite Systems)[t]", group.Aliases, StringComparer.Ordinal);
    }

    /// <summary>Distinct games must never be merged by annotation stripping.</summary>
    [Fact]
    public void GroupByName_KeepsDistinctGamesSeparate()
    {
        var groups = CoverCatalogPlanner.GroupByName(
        [
            new CoverCatalogCandidate("1942", ".png", 100, 320, 600),
            new CoverCatalogCandidate("1943 - The Battle of Midway", ".png", 100, 320, 600)
        ]);

        Assert.Equal(2, groups.Count);
    }

    /// <summary>Nested annotations are stripped like the local cover matcher.</summary>
    [Fact]
    public void NormalizeGameName_StripsNestedAnnotationsAndKeepsOriginalWhenEmpty()
    {
        Assert.Equal("Game", CoverCatalogPlanner.NormalizeGameName("Game (USA (Rev 1)) [b]"));
        Assert.Equal("(USA)", CoverCatalogPlanner.NormalizeGameName("(USA)"));
    }

    /// <summary>The unannotated exact name wins when the images are otherwise equivalent.</summary>
    [Fact]
    public void SelectCanonical_PrefersTheUnannotatedName()
    {
        var candidate = CoverCatalogPlanner.SelectCanonical(
        [
            new CoverCatalogCandidate("1942 (1986)(Elite Systems)", ".png", 100, 320, 600),
            new CoverCatalogCandidate("1942", ".png", 100, 320, 600)
        ]);

        Assert.Equal("1942", candidate.BaseName);
    }

    /// <summary>Box art (portrait) is preferred over a landscape in-game snapshot.</summary>
    [Fact]
    public void SelectCanonical_PrefersPortraitBoxArtOverLandscapeSnapshot()
    {
        var candidate = CoverCatalogPlanner.SelectCanonical(
        [
            new CoverCatalogCandidate("berzerk", ".png", 7_000, 336, 225),
            new CoverCatalogCandidate("BerZerk (USA)", ".png", 30_000, 439, 600)
        ]);

        Assert.Equal("BerZerk (USA)", candidate.BaseName);
    }

    /// <summary>For two portrait names, PNG is preferred as the source format.</summary>
    [Fact]
    public void SelectCanonical_PrefersPngForEqualNames()
    {
        var candidate = CoverCatalogPlanner.SelectCanonical(
        [
            new CoverCatalogCandidate("1942", ".jpg", 500, 320, 600),
            new CoverCatalogCandidate("1942", ".png", 100, 320, 600)
        ]);

        Assert.Equal(".png", candidate.Extension);
    }

    /// <summary>Slugs are lower-case, URL-safe and accent-insensitive.</summary>
    [Theory]
    [InlineData("Nintendo NES", "nintendo-nes")]
    [InlineData("Atari 8-Bit", "atari-8-bit")]
    [InlineData("NEC PC-FX", "nec-pc-fx")]
    [InlineData("Sony PlayStation 1", "sony-playstation-1")]
    [InlineData("Pokémon Mini", "pokemon-mini")]
    public void CreateSystemSlug_ProducesUrlSafeSlugs(string name, string expected)
    {
        Assert.Equal(expected, CoverCatalogPlanner.CreateSystemSlug(name));
    }

    /// <summary>Empty or punctuation-only system names still get a usable folder.</summary>
    [Fact]
    public void CreateSystemSlug_FallsBackForEmptyNames()
    {
        Assert.Equal("system", CoverCatalogPlanner.CreateSystemSlug(""));
        Assert.Equal("system", CoverCatalogPlanner.CreateSystemSlug("   "));
        Assert.Equal("system", CoverCatalogPlanner.CreateSystemSlug("---"));
    }
}
