using SimpleLauncher.Core.Services.FindCoverImage;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the Jaro-Winkler similarity algorithm used in cover image matching.
/// </summary>
public class FindCoverImageJaroWinklerTests
{
    // Jaro-Winkler Similarity Tests

    /// <summary>
    ///     Verifies that identical strings return a similarity of 1.0.
    /// </summary>
    [Fact]
    public void JaroWinklerIdenticalStringsReturns1()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("hello", "hello");
        Assert.Equal(1.0, result, 10);
    }

    /// <summary>
    ///     Verifies that two empty strings return a similarity of 1.0.
    /// </summary>
    [Fact]
    public void JaroWinklerEmptyStringsReturns1()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("", "");
        Assert.Equal(1.0, result, 10);
    }

    /// <summary>
    ///     Verifies that two null values return a similarity of 1.0.
    /// </summary>
    [Fact]
    public void JaroWinklerBothNullReturns1()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity(null!, null!);
        Assert.Equal(1.0, result, 10);
    }

    /// <summary>
    ///     Verifies that one empty and one null string return a similarity of 1.0.
    /// </summary>
    [Fact]
    public void JaroWinklerOneEmptyOneNullReturns1()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("", null!);
        Assert.Equal(1.0, result, 10);
    }

    /// <summary>
    ///     Verifies that comparing a non-empty string with an empty string returns 0.0.
    /// </summary>
    [Fact]
    public void JaroWinklerOneEmptyReturns0()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("hello", "");
        Assert.Equal(0.0, result, 10);
    }

    /// <summary>
    ///     Verifies that comparing a non-empty string with null returns 0.0.
    /// </summary>
    [Fact]
    public void JaroWinklerOneNullReturns0()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("hello", null!);
        Assert.Equal(0.0, result, 10);
    }

    /// <summary>
    ///     Verifies that completely different strings return a low similarity score.
    /// </summary>
    [Fact]
    public void JaroWinklerCompletelyDifferentReturnsLow()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("abc", "xyz");
        Assert.True(result < 0.5);
    }

    /// <summary>
    ///     Verifies that similar strings return a high similarity score above 0.9.
    /// </summary>
    [Fact]
    public void JaroWinklerSimilarStringsReturnsHigh()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("mario", "marion");
        Assert.True(result > 0.9);
    }

    /// <summary>
    ///     Verifies that the similarity calculation is case-insensitive.
    /// </summary>
    [Fact]
    public void JaroWinklerIsCaseInsensitive()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("HELLO", "hello");
        Assert.Equal(1.0, result, 10);
    }

    /// <summary>
    ///     Verifies that the prefix bonus is applied consistently for strings with the same prefix.
    /// </summary>
    [Fact]
    public void JaroWinklerPrefixBonusApplied()
    {
        var noPrefix = FindCoverImageService.CalculateJaroWinklerSimilarity("abcde", "abcxy");
        var withPrefix = FindCoverImageService.CalculateJaroWinklerSimilarity("abcde", "abcxy");
        // Both should be equal since prefix is the same
        Assert.Equal(noPrefix, withPrefix, 10);
    }

    /// <summary>
    ///     Verifies that identical single characters score 1.0 (the match window is clamped
    ///     at zero; see CORE-31).
    /// </summary>
    [Fact]
    public void JaroWinklerSingleCharacterMatch()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("a", "a");
        Assert.Equal(1.0, result, 10);
    }

    /// <summary>
    ///     Verifies that a single non-matching character returns 0.0.
    /// </summary>
    [Fact]
    public void JaroWinklerSingleCharacterMismatch()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("a", "b");
        Assert.Equal(0.0, result, 10);
    }

    /// <summary>
    ///     Verifies that transposed characters still yield a high similarity score.
    /// </summary>
    [Fact]
    public void JaroWinklerTranspositions()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("martha", "marhta");
        Assert.True(result > 0.9);
    }

    /// <summary>
    ///     Verifies that the similarity calculation is symmetric (order of arguments does not matter).
    /// </summary>
    [Fact]
    public void JaroWinklerSymmetric()
    {
        var r1 = FindCoverImageService.CalculateJaroWinklerSimilarity("hello", "world");
        var r2 = FindCoverImageService.CalculateJaroWinklerSimilarity("world", "hello");
        Assert.Equal(r1, r2, 10);
    }

    /// <summary>
    ///     Verifies that identical long strings return a similarity of 1.0.
    /// </summary>
    [Fact]
    public void JaroWinklerLongStrings()
    {
        const string s1 = "super mario bros world edition";
        const string s2 = "super mario bros world edition";
        Assert.Equal(1.0, FindCoverImageService.CalculateJaroWinklerSimilarity(s1, s2), 10);
    }

    /// <summary>
    ///     Verifies that similar game names produce a high similarity score above 0.85.
    /// </summary>
    [Fact]
    public void JaroWinklerGameNames()
    {
        var result =
            FindCoverImageService.CalculateJaroWinklerSimilarity("street fighter ii", "street fighter ii turbo");
        Assert.True(result > 0.85);
    }

    /// <summary>
    ///     Verifies that strings with numbers produce a high similarity score when close.
    /// </summary>
    [Fact]
    public void JaroWinklerWithNumbers()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("game123", "game124");
        Assert.True(result > 0.9);
    }

    /// <summary>
    ///     Verifies that strings with special characters are handled correctly.
    /// </summary>
    [Fact]
    public void JaroWinklerWithSpecialChars()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("mega-man x", "mega-man x");
        Assert.Equal(1.0, result, 10);
    }

    /// <summary>
    ///     Verifies that the max prefix length of 4 is applied correctly in the algorithm.
    /// </summary>
    [Fact]
    public void JaroWinklerMaxLengthPrefixIs4()
    {
        // The algorithm uses MaxPrefixLength = 4
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("abcdefgh", "abcdeXYZ");
        Assert.True(result > 0.5);
    }

    /// <summary>
    ///     Verifies that the similarity result is always between 0.0 and 1.0.
    /// </summary>
    [Fact]
    public void JaroWinklerReturnsBetween0And1()
    {
        var result = FindCoverImageService.CalculateJaroWinklerSimilarity("test", "testing");
        Assert.InRange(result, 0.0, 1.0);
    }

    // Annotation Stripping Tests

    /// <summary>
    ///     Verifies that parenthetical annotations are removed from filenames.
    /// </summary>
    [Fact]
    public void StripAnnotations_Parentheses_RemovesContent()
    {
        var result = FindCoverImageService.StripAnnotations("007 - Everything or Nothing (Asia) (En)");
        Assert.Equal("007 - Everything or Nothing", result);
    }

    /// <summary>
    ///     Verifies that square bracket annotations are removed from filenames.
    /// </summary>
    [Fact]
    public void StripAnnotations_SquareBrackets_RemovesContent()
    {
        var result = FindCoverImageService.StripAnnotations("Game [v1.0]");
        Assert.Equal("Game", result);
    }

    /// <summary>
    ///     Verifies that curly brace annotations are removed from filenames.
    /// </summary>
    [Fact]
    public void StripAnnotations_CurlyBraces_RemovesContent()
    {
        var result = FindCoverImageService.StripAnnotations("Game {test}");
        Assert.Equal("Game", result);
    }

    /// <summary>
    ///     Verifies that mixed annotation types are all removed.
    /// </summary>
    [Fact]
    public void StripAnnotations_Mixed_RemovesAll()
    {
        var result = FindCoverImageService.StripAnnotations("Game (USA) [v1] {test}");
        Assert.Equal("Game", result);
    }

    /// <summary>
    ///     Verifies that filenames without annotations are returned unchanged.
    /// </summary>
    [Fact]
    public void StripAnnotations_NoAnnotations_ReturnsUnchanged()
    {
        var result = FindCoverImageService.StripAnnotations("Game");
        Assert.Equal("Game", result);
    }

    /// <summary>
    ///     Verifies that null input returns null.
    /// </summary>
    [Fact]
    public void StripAnnotations_Null_ReturnsNull()
    {
        var result = FindCoverImageService.StripAnnotations(null!);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that empty input returns empty.
    /// </summary>
    [Fact]
    public void StripAnnotations_Empty_ReturnsEmpty()
    {
        var result = FindCoverImageService.StripAnnotations("");
        Assert.Equal("", result);
    }

    /// <summary>
    ///     Verifies that when the entire filename is annotations, the original is returned.
    /// </summary>
    [Fact]
    public void StripAnnotations_OnlyAnnotations_ReturnsOriginal()
    {
        var result = FindCoverImageService.StripAnnotations("(test)");
        Assert.Equal("(test)", result);
    }

    /// <summary>
    ///     Verifies that trailing whitespace and dots are trimmed after stripping.
    /// </summary>
    [Fact]
    public void StripAnnotations_TrailingDotsAndSpaces_Trims()
    {
        var result = FindCoverImageService.StripAnnotations("Game (USA) ");
        Assert.Equal("Game", result);
    }

    /// <summary>
    ///     Verifies that annotation stripping enables high similarity between ROM and image names.
    /// </summary>
    [Fact]
    public void FuzzyMatch_WithAnnotationStripping_MatchesCorrectly()
    {
        var romName = FindCoverImageService.StripAnnotations("007 - Everything or Nothing (Asia) (En)");
        var imageName = FindCoverImageService.StripAnnotations("007 - Everything or Nothing");
        var similarity = FindCoverImageService.CalculateJaroWinklerSimilarity(
            romName.ToLowerInvariant(), imageName.ToLowerInvariant());
        Assert.True(similarity > 0.95, $"Expected similarity > 0.95 but got {similarity}");
    }

    /// <summary>
    ///     Verifies that multiple region variants all match the same base name.
    /// </summary>
    [Fact]
    public void StripAnnotations_MultipleRegionVariants_AllMatchBase()
    {
        var rom1 = FindCoverImageService.StripAnnotations("007 - Everything or Nothing (Asia) (En)");
        var rom2 = FindCoverImageService.StripAnnotations("007 - Everything or Nothing (Europe) (En,Es,It,Nl,Sv)");
        var rom3 = FindCoverImageService.StripAnnotations("007 - Everything or Nothing (Europe) (Fr,De)");
        var rom4 = FindCoverImageService.StripAnnotations("007 - Everything or Nothing (USA)");
        var image = FindCoverImageService.StripAnnotations("007 - Everything or Nothing");

        Assert.Equal(image, rom1);
        Assert.Equal(image, rom2);
        Assert.Equal(image, rom3);
        Assert.Equal(image, rom4);
    }

    /// <summary>
    ///     Verifies that nested parentheses are fully stripped.
    /// </summary>
    [Fact]
    public void StripAnnotations_NestedParentheses_HandlesGracefully()
    {
        var result = FindCoverImageService.StripAnnotations("Game (Region (Sub))");
        Assert.Equal("Game", result);
    }

    /// <summary>
    ///     Verifies that mixed parentheses and square brackets (GoodTools-style annotations) are all stripped,
    ///     leaving the base ROM name for cover image matching.
    /// </summary>
    [Fact]
    public void StripAnnotations_ParensThenSquareBrackets_AllRemoved()
    {
        const string baseName = "007 - Live and Let Die";

        var r1 = FindCoverImageService.StripAnnotations("007 - Live and Let Die (1988)(Domark)");
        var r2 = FindCoverImageService.StripAnnotations("007 - Live and Let Die (1988)(Domark)[cr XOR][t  2 XOR]");
        var r3 = FindCoverImageService.StripAnnotations("007 - Live and Let Die (1988)(Domark)[cr Celtic Gang]");

        Assert.Equal(baseName, r1);
        Assert.Equal(baseName, r2);
        Assert.Equal(baseName, r3);
    }
}