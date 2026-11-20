using System.Text.Json;
using SimpleLauncher.Avalonia.Services.GameScan;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the PowerShell JSON repair used by the Microsoft Store scanner (bug #68895).
/// </summary>
public class ScanMicrosoftStoreGamesJsonTests
{
    [Fact]
    public void SanitizeJsonEscapesRepairsInvalidEscapeBeforeNonAsciiCharacter()
    {
        const string invalidJson = "[{\"Name\":\"A\\Å\"}]";

        var sanitized = ScanMicrosoftStoreGames.SanitizeJsonEscapes(invalidJson);

        using var document = JsonDocument.Parse(sanitized);
        Assert.Equal("A\\Å", document.RootElement[0].GetProperty("Name").GetString());
    }

    [Fact]
    public void SanitizeJsonEscapesKeepsValidEscapes()
    {
        const string validJson = "[{\"Name\":\"Line\\nBreak\\u00C5\"}]";

        Assert.Equal(validJson, ScanMicrosoftStoreGames.SanitizeJsonEscapes(validJson));
    }

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
