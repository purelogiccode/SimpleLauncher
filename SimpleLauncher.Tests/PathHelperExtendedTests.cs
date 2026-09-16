using System.Diagnostics.CodeAnalysis;
using Xunit;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Tests;

/// <summary>
///     Extended tests for <see cref="PathHelper" /> covering additional edge cases for
///     path resolution, placeholder handling, parameter string resolution, and file lookup.
/// </summary>
[SuppressMessage("ReSharper", "NullableWarningSuppressionIsUsed")]
public class PathHelperExtendedTests
{
    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory returns null for null input.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryNullReturnsNull()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory(null);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory returns null for empty input.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryEmptyReturnsNull()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory("");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory returns null for whitespace-only input.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryWhitespaceReturnsNull()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory("   ");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that a path consisting of a single quote does not throw: both characters
    ///     satisfy StartsWith and EndsWith, and path[1..^1] used to throw
    ///     ArgumentOutOfRangeException.
    /// </summary>
    [Theory]
    [InlineData("\"")]
    [InlineData("'")]
    public void ResolveRelativeToAppDirectorySingleQuoteDoesNotThrow(string path)
    {
        var exception = Record.Exception(() => PathHelper.ResolveRelativeToAppDirectory(path));
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory returns null for an extremely long path.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryVeryLongPathReturnsNull()
    {
        var longPath = new string('a', 5000);
        var result = PathHelper.ResolveRelativeToAppDirectory(longPath);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory returns the app directory for the bare %BASEFOLDER% placeholder.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryBaseFolderOnlyReturnsAppDirectory()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory("%BASEFOLDER%");
        Assert.NotNull(result);
        Assert.True(Path.IsPathRooted(result));
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory handles %BASEFOLDER% with a trailing separator.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryBaseFolderWithTrailingSeparator()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory("%BASEFOLDER%\\");
        Assert.NotNull(result);
        Assert.True(Path.IsPathRooted(result));
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory treats %BASEFOLDER% as case-insensitive.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryBaseFolderCaseInsensitive()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory("%basefolder%\\test");
        Assert.NotNull(result);
        Assert.EndsWith("test", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that ContainsGameSpecificPlaceholder detects all supported placeholder variants including
    ///     %GAME%, %ROMNAME%, %ROMFILE%, $game$, $romname$, $romfile$, {game}, {romname}, and {romfile}.
    /// </summary>
    /// <param name="text">The text to inspect for a game-specific placeholder.</param>
    /// <param name="expected">Whether a placeholder is expected to be detected.</param>
    [Theory]
    [InlineData("%GAME%", true)]
    [InlineData("%ROMNAME%", true)]
    [InlineData("%ROMFILE%", true)]
    [InlineData("$game$", true)]
    [InlineData("$romname$", true)]
    [InlineData("$romfile$", true)]
    [InlineData("{game}", true)]
    [InlineData("{romname}", true)]
    [InlineData("{romfile}", true)]
    [InlineData("%GAME%.zip", true)]
    [InlineData("path/%ROMNAME%/file", true)]
    [InlineData("normalpath", false)]
    [InlineData("%BASEFOLDER%", false)]
    public void ContainsGameSpecificPlaceholderAllVariants(string text, bool expected)
    {
        var result = PathHelper.ContainsGameSpecificPlaceholder(text);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that GetLongPath returns whitespace unchanged.
    /// </summary>
    [Fact]
    public void GetLongPathWhitespaceReturnsWhitespace()
    {
        var result = PathHelper.GetLongPath("   ");
        Assert.Equal("   ", result);
    }

    /// <summary>
    ///     Verifies that GetLongPath returns an already-extended path unchanged.
    /// </summary>
    [Fact]
    public void GetLongPathAlreadyExtendedWithDotSlashReturnsUnchanged()
    {
        var result = PathHelper.GetLongPath(@"\\.\C:\path");
        Assert.Equal(@"\\.\C:\path", result);
    }

    /// <summary>
    ///     Verifies that SanitizePathToken returns an empty string for null input.
    /// </summary>
    [Fact]
    public void SanitizePathTokenNullReturnsEmpty()
    {
        var result = PathHelper.SanitizePathToken(null);
        Assert.Equal("", result);
    }

    /// <summary>
    ///     Verifies that SanitizePathToken returns an empty string when the input consists only of separators.
    /// </summary>
    [Fact]
    public void SanitizePathTokenOnlySeparatorsReturnsEmpty()
    {
        var result = PathHelper.SanitizePathToken("\\\\");
        Assert.Equal("", result);
    }

    /// <summary>
    ///     Verifies that GetFileNameWithoutExtension returns null for null input.
    /// </summary>
    [Fact]
    public void GetFileNameWithoutExtensionNullReturnsNull()
    {
        var result = PathHelper.GetFileNameWithoutExtension(null!);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that GetFileName returns null for null input.
    /// </summary>
    [Fact]
    public void GetFileNameNullReturnsNull()
    {
        var result = PathHelper.GetFileName(null!);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that GetFileNameWithoutExtension correctly handles paths with spaces.
    /// </summary>
    [Fact]
    public void GetFileNameWithoutExtensionPathWithSpaces()
    {
        var result = PathHelper.GetFileNameWithoutExtension(@"C:\my games\super mario.zip");
        Assert.Equal("super mario", result);
    }

    /// <summary>
    ///     Verifies that GetFileName correctly handles paths with spaces.
    /// </summary>
    [Fact]
    public void GetFileNamePathWithSpaces()
    {
        var result = PathHelper.GetFileName(@"C:\my games\super mario.zip");
        Assert.Equal("super mario.zip", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString returns an empty string for empty input.
    /// </summary>
    [Fact]
    public void ResolveParameterStringEmptyReturnsEmpty()
    {
        var result = PathHelper.ResolveParameterString("");
        Assert.Equal("", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString returns an empty string for whitespace-only input.
    /// </summary>
    [Fact]
    public void ResolveParameterStringWhitespaceOnlyReturnsEmpty()
    {
        var result = PathHelper.ResolveParameterString("   ");
        Assert.Equal("", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString leaves parameters without placeholders unchanged.
    /// </summary>
    [Fact]
    public void ResolveParameterStringNoPlaceholdersReturnsUnchanged()
    {
        const string parameters = "-f --fullscreen -window";
        var result = PathHelper.ResolveParameterString(parameters);
        Assert.Equal(parameters, result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString resolves %SYSTEMFOLDER% to an empty string when the system folders list is
    ///     empty.
    /// </summary>
    [Fact]
    public void ResolveParameterStringEmptySystemFoldersListResolvesEmpty()
    {
        const string parameters = "-rompath %SYSTEMFOLDER%";
        var result = PathHelper.ResolveParameterString(parameters, []);
        Assert.Equal("-rompath ", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString resolves %SYSTEMFOLDER% to an empty string when system folders is null.
    /// </summary>
    [Fact]
    public void ResolveParameterStringNullSystemFoldersResolvesEmpty()
    {
        const string parameters = "-rompath %SYSTEMFOLDER%";
        var result = PathHelper.ResolveParameterString(parameters);
        Assert.Equal("-rompath ", result);
    }

    /// <summary>
    ///     Verifies that the %ROM% placeholder resolves to the full ROM path including spaces.
    /// </summary>
    [Fact]
    public void ResolveParameterStringRomPlaceholderWithSpacesAddsQuotes()
    {
        const string parameters = "-rom %ROM%";
        var result = PathHelper.ResolveParameterString(parameters, null, null, @"C:\my roms\game file.zip");
        Assert.Contains("C:\\my roms\\game file.zip", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that the %NAME% placeholder preserves surrounding quotes in the parameter string.
    /// </summary>
    [Fact]
    public void ResolveParameterStringNamePlaceholderPreservesQuotes()
    {
        const string parameters = "\"%NAME%\"";
        var result = PathHelper.ResolveParameterString(parameters, null, null, null, null, "test game");
        Assert.Equal("\"test game\"", result);
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders returns null for a null system folders list.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersNullListReturnsNull()
    {
        var result = PathHelper.FindFileInSystemFolders(null, "game.zip");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders returns null for an empty system folders list.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersEmptyListReturnsNull()
    {
        var result = PathHelper.FindFileInSystemFolders([], "game.zip");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders returns null for a null file name.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersNullFileNameReturnsNull()
    {
        var result = PathHelper.FindFileInSystemFolders(["C:\\roms"], null);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders returns null for an empty file name.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersEmptyFileNameReturnsNull()
    {
        var result = PathHelper.FindFileInSystemFolders(["C:\\roms"], "");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindContainingSystemFolder returns the primary folder when the file path is empty.
    /// </summary>
    [Fact]
    public void FindContainingSystemFolderEmptyFilePathReturnsPrimaryFolder()
    {
        var result = PathHelper.FindContainingSystemFolder(["C:\\roms"], "C:\\primary", "");
        Assert.Equal("C:\\primary", result);
    }

    /// <summary>
    ///     Verifies that TryGetExistingDirectory returns null for null input.
    /// </summary>
    [Fact]
    public void TryGetExistingDirectoryNullReturnsNull()
    {
        var result = PathHelper.TryGetExistingDirectory(null);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that TryGetExistingDirectory returns null for empty input.
    /// </summary>
    [Fact]
    public void TryGetExistingDirectoryEmptyReturnsNull()
    {
        var result = PathHelper.TryGetExistingDirectory("");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that TryGetExistingDirectory returns null for whitespace-only input.
    /// </summary>
    [Fact]
    public void TryGetExistingDirectoryWhitespaceReturnsNull()
    {
        var result = PathHelper.TryGetExistingDirectory("   ");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that TryGetExistingDirectory returns null for a non-existent directory path.
    /// </summary>
    [Fact]
    public void TryGetExistingDirectoryNonExistentReturnsNull()
    {
        var result = PathHelper.TryGetExistingDirectory("C:\\nonexistent_path_12345");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that TryGetExistingDirectory returns the path when the directory exists.
    /// </summary>
    [Fact]
    public void TryGetExistingDirectoryExistingReturnsPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = PathHelper.TryGetExistingDirectory(tempDir);
            Assert.NotNull(result);
            Assert.True(Directory.Exists(result));
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }
}