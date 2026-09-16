using SimpleLauncher.Core.Services.RetroAchievements;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="RetroAchievementsSystemMatcher" /> covering system name aliasing,
///     official name validation, supported system enumeration, and system ID lookups.
/// </summary>
public class RetroAchievementsSystemMatcherTests
{
    private readonly RetroAchievementsSystemMatcher _matcher = new(new NoOpLogger(), Log.Logger);

    /// <summary>
    ///     Verifies that GetBestMatchSystemName maps known system aliases to their canonical RetroAchievements names.
    /// </summary>
    /// <param name="input">The system name or alias to match.</param>
    /// <param name="expected">The expected canonical RetroAchievements system name.</param>
    [Theory]
    [InlineData("snes", "super nintendo entertainment system")]
    [InlineData("SNES", "super nintendo entertainment system")]
    [InlineData("Super Nintendo", "super nintendo entertainment system")]
    [InlineData("n64", "nintendo 64")]
    [InlineData("Nintendo 64", "nintendo 64")]
    [InlineData("gba", "game boy advance")]
    [InlineData("genesis", "genesis/mega drive")]
    [InlineData("sega genesis", "genesis/mega drive")]
    [InlineData("ps1", "playstation")]
    [InlineData("playstation", "playstation")]
    [InlineData("arcade", "arcade")]
    [InlineData("mame", "arcade")]
    [InlineData("dreamcast", "dreamcast")]
    [InlineData("sega dreamcast", "dreamcast")]
    public void GetBestMatchSystemNameKnownAliasReturnsExpectedKey(string input, string expected)
    {
        var result = _matcher.GetBestMatchSystemName(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that GetBestMatchSystemName handles null, empty, and whitespace inputs gracefully.
    /// </summary>
    [Fact]
    public void GetBestMatchSystemNameNullOrWhitespaceReturnsOriginal()
    {
        Assert.Null(_matcher.GetBestMatchSystemName(null!));
        Assert.Equal("", _matcher.GetBestMatchSystemName(""));
        Assert.Equal("   ", _matcher.GetBestMatchSystemName("   "));
    }

    /// <summary>
    ///     Verifies that IsOfficialSystemName correctly identifies known official system names.
    /// </summary>
    /// <param name="name">The system name to check.</param>
    /// <param name="expected">Whether the name is expected to be an official system name.</param>
    [Theory]
    [InlineData("super nintendo entertainment system", true)]
    [InlineData("playstation", true)]
    [InlineData("arcade", true)]
    [InlineData("unknown console xyz", false)]
    public void IsOfficialSystemNameReturnsExpected(string name, bool expected)
    {
        var result = _matcher.IsOfficialSystemName(name);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that GetSupportedSystemNames returns a non-empty list with no duplicates.
    /// </summary>
    [Fact]
    public void GetSupportedSystemNamesReturnsNonEmptyList()
    {
        var result = _matcher.GetSupportedSystemNames();
        Assert.NotEmpty(result);
        Assert.Equal(result.Count, result.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    ///     Verifies that GetExactAliasMatch returns the expected canonical name for known aliases.
    /// </summary>
    /// <param name="input">The alias to look up.</param>
    /// <param name="expected">The expected canonical system name for the alias.</param>
    [Theory]
    [InlineData("snes", "super nintendo entertainment system")]
    [InlineData("gb", "game boy")]
    [InlineData("n64", "nintendo 64")]
    public void GetExactAliasMatchReturnsExpected(string input, string expected)
    {
        var result = _matcher.GetExactAliasMatch(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that GetExactAliasMatch returns null for an unknown alias.
    /// </summary>
    [Fact]
    public void GetExactAliasMatchUnknownReturnsNull()
    {
        var result = _matcher.GetExactAliasMatch("unknown");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that GetBestMatchSystemName returns a non-null result for a partial alias like "Nintendo".
    /// </summary>
    [Fact]
    public void GetBestMatchSystemNameWithAlias()
    {
        var result = _matcher.GetBestMatchSystemName("Nintendo");
        Assert.NotNull(result);
    }

    /// <summary>
    ///     Verifies that GetBestMatchSystemName normalizes an unknown system name to lowercase.
    /// </summary>
    [Fact]
    public void GetBestMatchSystemNameWithUnknownReturnsNormalizedInput()
    {
        var result = _matcher.GetBestMatchSystemName("UnknownSystem12345");
        Assert.Equal("unknownsystem12345", result);
    }

    /// <summary>
    ///     Verifies that IsOfficialSystemName returns true for a valid official name.
    /// </summary>
    [Fact]
    public void IsOfficialSystemNameWithValidName()
    {
        var result = _matcher.IsOfficialSystemName("nintendo entertainment system");
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that IsOfficialSystemName returns false for an unrecognized system name.
    /// </summary>
    [Fact]
    public void IsOfficialSystemNameWithInvalidName()
    {
        var result = _matcher.IsOfficialSystemName("UnknownSystem12345");
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that GetSupportedSystemNames includes well-known system names.
    /// </summary>
    [Fact]
    public void GetSupportedSystemNamesContainsKnownSystems()
    {
        var names = _matcher.GetSupportedSystemNames();
        Assert.Contains("nintendo entertainment system", names, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Verifies that GetSystemId returns a positive integer for a known system.
    /// </summary>
    [Fact]
    public void GetSystemIdWithValidSystem()
    {
        var id = _matcher.GetSystemId("NES");
        Assert.True(id > 0);
    }

    /// <summary>
    ///     Verifies that GetSystemId returns -1 for an unknown system name.
    /// </summary>
    [Fact]
    public void GetSystemIdWithInvalidSystemReturnsMinusOne()
    {
        var id = _matcher.GetSystemId("UnknownSystem12345");
        Assert.Equal(-1, id);
    }

    /// <summary>
    ///     Verifies that IsSystemInMappings returns true for a known system alias.
    /// </summary>
    [Fact]
    public void IsSystemInMappingsWithValidSystem()
    {
        var result = _matcher.IsSystemInMappings("NES");
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that IsSystemInMappings returns false for an unknown system name.
    /// </summary>
    [Fact]
    public void IsSystemInMappingsWithInvalidSystem()
    {
        var result = _matcher.IsSystemInMappings("UnknownSystem12345");
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that the number of supported systems is at least 10.
    /// </summary>
    [Fact]
    public void SupportedSystemsCountIsReasonable()
    {
        var names = _matcher.GetSupportedSystemNames();
        Assert.True(names.Count >= 10, $"Expected at least 10 supported systems, got {names.Count}");
    }

    /// <summary>
    ///     Verifies that IsSystemInMappings returns the expected result for known system aliases.
    /// </summary>
    /// <param name="input">The system name or alias to look up.</param>
    /// <param name="expected">Whether the system is expected to be in the mappings.</param>
    [Theory]
    [InlineData("snes", true)]
    [InlineData("super nintendo", true)]
    [InlineData("nintendo 64", true)]
    public void IsSystemInMappingsReturnsExpected(string input, bool expected)
    {
        var result = _matcher.IsSystemInMappings(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that GetSystemId returns the expected ID for known system aliases.
    /// </summary>
    /// <param name="input">The system name or alias to look up.</param>
    /// <param name="expected">The expected RetroAchievements system ID.</param>
    [Theory]
    [InlineData("snes", 3)]
    [InlineData("n64", 2)]
    [InlineData("playstation", 12)]
    public void GetSystemIdKnownSystemReturnsExpectedId(string input, int expected)
    {
        var result = _matcher.GetSystemId(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies the single source of truth for hash support: systems with a usable console id
    ///     and known hash logic are supported, while unknown-hash-logic and pseudo systems are not.
    ///     "Arcade" must not be rejected because "SEGA_arcade" is unsupported, and Mega Duck /
    ///     Naomi (console ids 69/27) are hashable by the bundled CLI.
    /// </summary>
    /// <param name="input">The system name to check.</param>
    /// <param name="expected">Whether the system is expected to be supported.</param>
    [Theory]
    [InlineData("Arcade", true)]
    [InlineData("MAME", true)]
    [InlineData("Mega Duck", true)]
    [InlineData("Sega Naomi", true)]
    [InlineData("Naomi", true)]
    [InlineData("NES", true)]
    [InlineData("SEGA_arcade", false)]
    [InlineData("Commodore 64", false)]
    [InlineData("PlayStation 3", false)]
    [InlineData("Microsoft Windows", false)]
    [InlineData("UnknownSystem12345", false)]
    [InlineData("", false)]
    public void IsSystemSupportedForHashingReturnsExpected(string input, bool expected)
    {
        var result = _matcher.IsSystemSupportedForHashing(input);
        Assert.Equal(expected, result);
    }
}