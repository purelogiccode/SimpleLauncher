using SimpleLauncher.Core.Services.RetroAchievements;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="RetroAchievementsSystemMatcher" /> covering system name aliasing,
///     official name validation, supported system enumeration, and system ID lookups.
/// </summary>
public class RetroAchievementsSystemMatcherTests
{
    private readonly RetroAchievementsSystemMatcher _matcher = new(new NoOpLogger());

    /// <summary>
    ///     Verifies that GetBestMatchSystemName maps known system aliases to their canonical RetroAchievements names.
    /// </summary>
    /// <param name="input">The system name or alias to match.</param>
    /// <param name="expected">The expected canonical RetroAchievements system name.</param>
    [Theory]
    [InlineData("snes", "super nintendo entertainment system")]
    [InlineData("SNES", "super nintendo entertainment system")]
    [InlineData("sfc", "super nintendo entertainment system")]
    [InlineData("SFC", "super nintendo entertainment system")]
    [InlineData("Super Famicom", "super nintendo entertainment system")]
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
    [InlineData("Xbox", true)]
    [InlineData("xbox", true)]
    [InlineData("DOS", true)]
    [InlineData("zeebo", true)]
    [InlineData("unknown console xyz", false)]
    public void IsOfficialSystemNameReturnsExpected(string name, bool expected)
    {
        var result = _matcher.IsOfficialSystemName(name);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that IsOfficialSystemName treats a null name as not-official instead of throwing.
    /// </summary>
    [Fact]
    public void IsOfficialSystemNameNullReturnsFalse()
    {
        Assert.False(_matcher.IsOfficialSystemName(null!));
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
    [InlineData("Xbox", true)]
    [InlineData("nintendo", false)]
    [InlineData("sega", false)]
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
    [InlineData("sfc", 3)]
    [InlineData("SFC", 3)]
    [InlineData("n64", 2)]
    [InlineData("playstation", 12)]
    public void GetSystemIdKnownSystemReturnsExpectedId(string input, int expected)
    {
        var result = _matcher.GetSystemId(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies the single source of truth for hash support against the bundled CLI's
    ///     HashEngine.FromFile switch: consoles with a FromFile case and a usable id are
    ///     supported, consoles without one (Wii U, PC-9800, Zeebo) and pseudo systems are
    ///     not. "Arcade" must not be rejected because "SEGA_arcade" is unsupported, and
    ///     Mega Duck / Naomi (console ids 69/27) are hashable by the bundled CLI.
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
    [InlineData("Commodore 64", true)]
    [InlineData("ZX Spectrum", true)]
    [InlineData("Oric", true)]
    [InlineData("Uzebox", true)]
    [InlineData("Arcadia 2001", true)]
    [InlineData("Wii U", false)]
    [InlineData("PC-9800", false)]
    [InlineData("Zeebo", false)]
    [InlineData("Xbox", false)]
    [InlineData("SEGA_arcade", false)]
    [InlineData("PlayStation 3", false)]
    [InlineData("Microsoft Windows", false)]
    [InlineData("UnknownSystem12345", false)]
    [InlineData("", false)]
    public void IsSystemSupportedForHashingReturnsExpected(string input, bool expected)
    {
        var result = _matcher.IsSystemSupportedForHashing(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     New aliases for previously unmatched system names (bugs 67744/67742 "Arcade RA",
    ///     68035 "Sega Super 32X", 67792 "Commodore Amiga CD32").
    /// </summary>
    [Theory]
    [InlineData("Arcade RA", "arcade")]
    [InlineData("Sega Super 32X", "32x")]
    [InlineData("Commodore Amiga CD32", "amiga")]
    [InlineData("Amiga CD32", "amiga")]
    public void GetBestMatchSystemNameNewAliasReturnsExpectedKey(string input, string expected)
    {
        var result = _matcher.GetBestMatchSystemName(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Classification fixes aligned with the bundled CLI: Sega Model 2 resolves to the
    ///     "unsupported" pseudo-system (the CLI has no Model 2 console; arcade must not steal
    ///     the name via the case-insensitive "sega_Model 2" alias), "DREAMCAST NAOMI" is an
    ///     arcade board (console 27, filename hash) rather than a Dreamcast disc, and
    ///     Famicom-cartridge names hash as NES (CLI console table: "NES/Famicom"), not FDS.
    /// </summary>
    /// <param name="input">The system name or alias to match.</param>
    /// <param name="expected">The expected canonical RetroAchievements system name.</param>
    [Theory]
    [InlineData("sega model 2", "unsupported")]
    [InlineData("Sega_Model 2", "unsupported")]
    [InlineData("DREAMCAST NAOMI", "naomi")]
    [InlineData("Nintendo - Family Computer", "nintendo entertainment system")]
    [InlineData("Nintendo Family Computer", "nintendo entertainment system")]
    [InlineData("Nintendo FC", "nintendo entertainment system")]
    [InlineData("Nintendo - Family Computer Disk System", "famicom disk system")]
    public void GetBestMatchSystemNameClassificationFixesReturnExpectedKey(string input, string expected)
    {
        var result = _matcher.GetBestMatchSystemName(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Famicom-cartridge aliases resolve to console id 7 (NES); only disk-system names
    ///     resolve to 81 (FDS).
    /// </summary>
    /// <param name="input">The system name or alias to look up.</param>
    /// <param name="expected">The expected RetroAchievements console id.</param>
    [Theory]
    [InlineData("Nintendo - Family Computer", 7)]
    [InlineData("Nintendo FC", 7)]
    [InlineData("famicom disk system", 81)]
    [InlineData("DREAMCAST NAOMI", 27)]
    public void GetSystemIdClassificationFixesReturnExpectedId(string input, int expected)
    {
        var result = _matcher.GetSystemId(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     The hash system-selection list only offers systems the CLI can hash: Wii U and
    ///     Commodore 64 must not both appear (Wii U has no FromFile hasher, C64 has one).
    /// </summary>
    [Fact]
    public void GetSupportedSystemNamesExcludesUnsupportedSystems()
    {
        var names = _matcher.GetSupportedSystemNames();

        Assert.DoesNotContain("wii u", names, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("pc-9800", names, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("zeebo", names, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsupported", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("commodore 64", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("arcade", names, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     The UI's "no system selected" placeholder (bugs 68042/68005/68032/67842/67823,
    ///     including encoding-mangled variants) is recognized regardless of language.
    /// </summary>
    [Theory]
    [InlineData("No system selected")]
    [InlineData("Aucun système sélectionné")]
    [InlineData("Aucun syst�me s�lectionn�")]
    [InlineData("No se ha seleccionado ningún sistema")]
    [InlineData("Nenhum sistema selecionado")]
    public void IsNoSystemSelectedPlaceholderReturnsTrue(string input)
    {
        Assert.True(RetroAchievementsSystemMatcher.IsNoSystemSelectedPlaceholder(
            input.Trim().ToLowerInvariant()));
    }

    /// <summary>
    ///     Real system names are not mistaken for the "no system selected" placeholder.
    /// </summary>
    [Theory]
    [InlineData("arcade")]
    [InlineData("atari 8-bit")]
    [InlineData("sega super 32x")]
    public void IsNoSystemSelectedPlaceholderReturnsFalseForRealSystems(string input)
    {
        Assert.False(RetroAchievementsSystemMatcher.IsNoSystemSelectedPlaceholder(
            input.Trim().ToLowerInvariant()));
    }
}