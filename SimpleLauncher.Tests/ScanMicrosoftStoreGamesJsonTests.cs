using System.Text.Json;
using SimpleLauncher.Services.GameScan;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the PowerShell JSON repair used by the Microsoft Store scanner (bug #68895).
/// </summary>
public class ScanMicrosoftStoreGamesJsonTests
{
    /// <summary>
    ///     Verifies that a raw backslash before a non-ASCII character is escaped so the
    ///     PowerShell output can be parsed.
    /// </summary>
    [Fact]
    public void SanitizeJsonEscapesRepairsInvalidEscapeBeforeNonAsciiCharacter()
    {
        const string invalidJson = "[{\"Name\":\"A\\Å\"}]";

        var sanitized = ScanMicrosoftStoreGames.SanitizeJsonEscapes(invalidJson);

        using var document = JsonDocument.Parse(sanitized);
        Assert.Equal("A\\Å", document.RootElement[0].GetProperty("Name").GetString());
    }

    /// <summary>
    ///     Verifies that valid JSON escape sequences are left untouched.
    /// </summary>
    [Fact]
    public void SanitizeJsonEscapesKeepsValidEscapes()
    {
        const string validJson = "[{\"Name\":\"Line\\nBreak\\u00C5\"}]";

        Assert.Equal(validJson, ScanMicrosoftStoreGames.SanitizeJsonEscapes(validJson));
    }

    /// <summary>
    ///     Verifies that a correctly escaped backslash before a non-ASCII character is not
    ///     escaped a second time.
    /// </summary>
    [Fact]
    public void SanitizeJsonEscapesKeepsEscapedBackslashBeforeNonAsciiCharacter()
    {
        const string validJson = "[{\"Name\":\"C:\\\\Å\"}]";

        var sanitized = ScanMicrosoftStoreGames.SanitizeJsonEscapes(validJson);

        Assert.Equal(validJson, sanitized);
        using var document = JsonDocument.Parse(sanitized);
        Assert.Equal("C:\\Å", document.RootElement[0].GetProperty("Name").GetString());
    }
}
