using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="TagOption" /> record covering equality, initialization, and display properties.
/// </summary>
public class TagOptionTests
{
    /// <summary>
    ///     Verifies that a TagOption can be created with Tag and Display properties.
    /// </summary>
    [Fact]
    public void TagOptionCanBeCreatedWithProperties()
    {
        var option = new TagOption("nes", "Nintendo NES");

        Assert.Equal("nes", option.Tag);
        Assert.Equal("Nintendo NES", option.Display);
    }

    /// <summary>
    ///     Verifies that two TagOption records with the same values are equal.
    /// </summary>
    [Fact]
    public void TagOptionRecordEquality()
    {
        var option1 = new TagOption("nes", "Nintendo NES");
        var option2 = new TagOption("nes", "Nintendo NES");

        Assert.Equal(option1, option2);
    }

    /// <summary>
    ///     Verifies that two TagOption records with different Tag values are not equal.
    /// </summary>
    [Fact]
    public void TagOptionRecordInequalityDifferentTag()
    {
        var option1 = new TagOption("nes", "Nintendo NES");
        var option2 = new TagOption("snes", "Nintendo NES");

        Assert.NotEqual(option1, option2);
    }

    /// <summary>
    ///     Verifies that two TagOption records with different Display values are not equal.
    /// </summary>
    [Fact]
    public void TagOptionRecordInequalityDifferentDisplay()
    {
        var option1 = new TagOption("nes", "Nintendo NES");
        var option2 = new TagOption("nes", "Super Nintendo");

        Assert.NotEqual(option1, option2);
    }

    /// <summary>
    ///     Verifies that TagOption supports empty string values.
    /// </summary>
    [Fact]
    public void TagOptionSupportsEmptyStrings()
    {
        var option = new TagOption("", "");

        Assert.Equal("", option.Tag);
        Assert.Equal("", option.Display);
    }

    /// <summary>
    ///     Verifies that TagOption supports Unicode characters in both Tag and Display.
    /// </summary>
    [Fact]
    public void TagOptionSupportsUnicode()
    {
        var option = new TagOption("ポケモン", "ポケットモンスター");

        Assert.Equal("ポケモン", option.Tag);
        Assert.Equal("ポケットモンスター", option.Display);
    }

    /// <summary>
    ///     Verifies that TagOption supports special characters in Tag and Display.
    /// </summary>
    [Fact]
    public void TagOptionSupportsSpecialCharacters()
    {
        var option = new TagOption("game-v1.0", "Game (v1.0) [!]");

        Assert.Equal("game-v1.0", option.Tag);
        Assert.Equal("Game (v1.0) [!]", option.Display);
    }

    /// <summary>
    ///     Verifies that two TagOption records with the same values produce the same hash code.
    /// </summary>
    [Fact]
    public void TagOptionWithMatchingValuesHaveSameHashCode()
    {
        var option1 = new TagOption("nes", "Nintendo NES");
        var option2 = new TagOption("nes", "Nintendo NES");

        Assert.Equal(option1.GetHashCode(), option2.GetHashCode());
    }

    /// <summary>
    ///     Verifies that TagOption instances can be stored in a list and queried.
    /// </summary>
    [Fact]
    public void TagOptionCanBeUsedInList()
    {
        var options = new List<TagOption>
        {
            new("nes", "Nintendo NES"),
            new("snes", "Super Nintendo"),
            new("genesis", "Sega Genesis")
        };

        Assert.Equal(3, options.Count);
        Assert.Contains(options, o => string.Equals(o.Tag, "nes", StringComparison.Ordinal));
        Assert.Contains(options, o => string.Equals(o.Tag, "snes", StringComparison.Ordinal));
        Assert.Contains(options, o => string.Equals(o.Tag, "genesis", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Verifies that TagOption can be used as a dictionary key.
    /// </summary>
    [Fact]
    public void TagOptionCanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<TagOption, int>
        {
            { new TagOption("nes", "Nintendo NES"), 1 },
            { new TagOption("snes", "Super Nintendo"), 2 }
        };

        Assert.Equal(1, dict[new TagOption("nes", "Nintendo NES")]);
        Assert.Equal(2, dict[new TagOption("snes", "Super Nintendo")]);
    }

    /// <summary>
    ///     Verifies that TagOption supports deconstruction into its Tag and Display components.
    /// </summary>
    [Fact]
    public void TagOptionCanBeDeconstructed()
    {
        var option = new TagOption("nes", "Nintendo NES");
        var (tag, display) = option;

        Assert.Equal("nes", tag);
        Assert.Equal("Nintendo NES", display);
    }
}