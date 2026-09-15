using Microsoft.Extensions.Configuration;
using SimpleLauncher.Services.SystemManager;
using Xunit;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="PathHelper" /> covering path sanitization, placeholder resolution,
///     relative path detection, system folder lookup, and file normalization.
/// </summary>
public class PathHelperTests
{
    /// <summary>
    ///     Verifies that SanitizePathToken removes trailing path separators from the input.
    /// </summary>
    /// <param name="input">The raw path token to sanitize.</param>
    /// <param name="expected">The expected sanitized token.</param>
    [Theory]
    [InlineData("C:\\MyFolder\\", "C:\\MyFolder")]
    [InlineData("C:\\MyFolder//", "C:\\MyFolder")]
    [InlineData("C:\\MyFolder", "C:\\MyFolder")]
    [InlineData("", "")]
    public void SanitizePathTokenRemovesTrailingSeparators(string input, string expected)
    {
        var result = PathHelper.SanitizePathToken(input);
        Assert.Equal(expected, result);
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
    ///     Bug #67079/#67080: the Avalonia folder guard compared against
    ///     <c>BaseDirectory + separator</c>, but <c>BaseDirectory</c> already ends with a
    ///     separator, so even in-app folders were rejected. The helper must treat a
    ///     trailing separator correctly.
    /// </summary>
    [Fact]
    public void IsPathContainedInBaseFolderHandlesTrailingSeparator()
    {
        var root = Path.Combine(Path.GetTempPath(), "SimpleLauncherApp");
        var baseFolderWithSeparator = root + Path.DirectorySeparatorChar;
        var inside = Path.Combine(root, "images", "Microsoft Xbox");
        var outside = Path.Combine(Path.GetTempPath(), "SimpleLauncherApp2", "images");

        Assert.True(PathHelper.IsPathContainedInBaseFolder(inside, baseFolderWithSeparator));
        Assert.True(PathHelper.IsPathContainedInBaseFolder(root, baseFolderWithSeparator));
        Assert.False(PathHelper.IsPathContainedInBaseFolder(outside, baseFolderWithSeparator));
    }

    /// <summary>
    ///     Verifies that GetFileNameWithoutExtension returns the file name without its extension.
    /// </summary>
    /// <param name="path">The path to extract the file name from.</param>
    /// <param name="expected">The expected file name without its extension.</param>
    [Theory]
    [InlineData("C:\\test.txt", "test")]
    [InlineData("C:\\folder\\file.zip", "file")]
    [InlineData("file", "file")]
    public void GetFileNameWithoutExtensionReturnsExpected(string path, string expected)
    {
        var result = PathHelper.GetFileNameWithoutExtension(path);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that GetFileName returns the file name with extension from a full path.
    /// </summary>
    /// <param name="path">The path to extract the file name from.</param>
    /// <param name="expected">The expected file name including its extension.</param>
    [Theory]
    [InlineData("C:\\test.txt", "test.txt")]
    [InlineData("C:\\folder\\file.zip", "file.zip")]
    [InlineData("file", "file")]
    public void GetFileNameReturnsExpected(string path, string expected)
    {
        var result = PathHelper.GetFileName(path);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that ContainsGameSpecificPlaceholder detects %GAME% and %ROMNAME% placeholders.
    /// </summary>
    /// <param name="text">The text to inspect for a game-specific placeholder.</param>
    /// <param name="expected">Whether a placeholder is expected to be detected.</param>
    [Theory]
    [InlineData("%GAME%", true)]
    [InlineData("%ROMNAME%", true)]
    [InlineData("C:\\test.txt", false)]
    [InlineData("-f", false)]
    [InlineData("", false)]
    public void ContainsGameSpecificPlaceholderReturnsExpected(string text, bool expected)
    {
        var result = PathHelper.ContainsGameSpecificPlaceholder(text);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that ContainsGameSpecificPlaceholder returns false for null input.
    /// </summary>
    [Fact]
    public void ContainsGameSpecificPlaceholderNullReturnsFalse()
    {
        var result = PathHelper.ContainsGameSpecificPlaceholder(null);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that GetLongPath produces the correct extended-length path prefix for various inputs.
    /// </summary>
    /// <param name="path">The path to convert to an extended-length path.</param>
    /// <param name="expected">The expected extended-length path.</param>
    [Theory]
    [InlineData("C:\\very\\long\\path", "\\\\?\\C:\\very\\long\\path")]
    [InlineData("\\\\server\\share", "\\\\?\\UNC\\server\\share")]
    [InlineData("\\\\?\\C:\\already\\extended", "\\\\?\\C:\\already\\extended")]
    [InlineData("", "")]
    public void GetLongPathReturnsExpected(string path, string expected)
    {
        var result = PathHelper.GetLongPath(path);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Verifies that GetLongPath returns null for null input.
    /// </summary>
    [Fact]
    public void GetLongPathNullReturnsNull()
    {
        var result = PathHelper.GetLongPath(null);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindContainingSystemFolder returns the primary folder when the game file is inside it.
    /// </summary>
    [Fact]
    public void FindContainingSystemFolderPrimaryFolderMatchReturnsPrimaryFolder()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var primaryFolder = Path.Combine(baseDir, "roms", "Arcade");
        var gameFile = Path.Combine(primaryFolder, "game.zip");

        try
        {
            Directory.CreateDirectory(primaryFolder);
            File.WriteAllText(gameFile, "dummy");

            var systemManager = new SystemManagerService
            {
                SystemFolders = [primaryFolder]
            };

            var result = PathHelper.FindContainingSystemFolder(systemManager.SystemFolders,
                systemManager.PrimarySystemFolder, gameFile);
            Assert.Equal(primaryFolder, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that FindContainingSystemFolder returns an additional folder when the game file is inside it.
    /// </summary>
    [Fact]
    public void FindContainingSystemFolderAdditionalFolderMatchReturnsAdditionalFolder()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var primaryFolder = Path.Combine(baseDir, "roms", "Arcade");
        var additionalFolder = Path.Combine(baseDir, "roms", "cps1");
        var gameFile = Path.Combine(additionalFolder, "sf2.zip");

        try
        {
            Directory.CreateDirectory(primaryFolder);
            Directory.CreateDirectory(additionalFolder);
            File.WriteAllText(gameFile, "dummy");

            var systemManager = new SystemManagerService
            {
                SystemFolders = [primaryFolder, additionalFolder]
            };

            var result = PathHelper.FindContainingSystemFolder(systemManager.SystemFolders,
                systemManager.PrimarySystemFolder, gameFile);
            Assert.Equal(additionalFolder, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that FindContainingSystemFolder returns the primary folder when no system folder matches.
    /// </summary>
    [Fact]
    public void FindContainingSystemFolderNoMatchReturnsPrimaryFolder()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var primaryFolder = Path.Combine(baseDir, "roms", "Arcade");
        var otherFolder = Path.Combine(baseDir, "roms", "cps1");
        var gameFile = Path.Combine(baseDir, "unrelated", "game.zip");

        try
        {
            Directory.CreateDirectory(primaryFolder);
            Directory.CreateDirectory(otherFolder);
            Directory.CreateDirectory(Path.Combine(baseDir, "unrelated"));
            File.WriteAllText(gameFile, "dummy");

            var systemManager = new SystemManagerService
            {
                SystemFolders = [primaryFolder, otherFolder]
            };

            var result = PathHelper.FindContainingSystemFolder(systemManager.SystemFolders,
                systemManager.PrimarySystemFolder, gameFile);
            Assert.Equal(primaryFolder, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that FindContainingSystemFolder matches a parent folder for files in nested subdirectories.
    /// </summary>
    [Fact]
    public void FindContainingSystemFolderNestedSubfolderMatchReturnsParentFolder()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var primaryFolder = Path.Combine(baseDir, "roms", "Arcade");
        var nestedFolder = Path.Combine(primaryFolder, "subfolder");
        var gameFile = Path.Combine(nestedFolder, "game.zip");

        try
        {
            Directory.CreateDirectory(nestedFolder);
            File.WriteAllText(gameFile, "dummy");

            var systemManager = new SystemManagerService
            {
                SystemFolders = [primaryFolder]
            };

            var result = PathHelper.FindContainingSystemFolder(systemManager.SystemFolders,
                systemManager.PrimarySystemFolder, gameFile);
            Assert.Equal(primaryFolder, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that FindContainingSystemFolder returns null when the system folders list is null.
    /// </summary>
    [Fact]
    public void FindContainingSystemFolderNullSystemManagerReturnsNull()
    {
        var result = PathHelper.FindContainingSystemFolder(null, null, "C:\\game.zip");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindContainingSystemFolder returns the primary folder when the file path is null.
    /// </summary>
    [Fact]
    public void FindContainingSystemFolderNullFilePathReturnsPrimaryFolder()
    {
        var systemManager = new SystemManagerService
        {
            SystemFolders = ["C:\\roms\\Arcade"]
        };

        var result =
            PathHelper.FindContainingSystemFolder(systemManager.SystemFolders, systemManager.PrimarySystemFolder, null);
        Assert.Equal("C:\\roms\\Arcade", result);
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory resolves the %BASEFOLDER% placeholder correctly.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryBaseFolderPlaceholderResolvesCorrectly()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory("%BASEFOLDER%\\roms\\test");
        Assert.NotNull(result);
        Assert.True(Path.IsPathRooted(result));
        Assert.EndsWith("roms\\test", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory returns an absolute path unchanged.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryAbsolutePathReturnsCanonicalPath()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory("C:\\Windows\\System32");
        Assert.Equal("C:\\Windows\\System32", result);
    }

    /// <summary>
    ///     Verifies that ResolveRelativeToAppDirectory resolves a relative path against the application directory.
    /// </summary>
    [Fact]
    public void ResolveRelativeToAppDirectoryRelativePathResolvesToAppDirectory()
    {
        var result = PathHelper.ResolveRelativeToAppDirectory("roms\\test");
        Assert.NotNull(result);
        Assert.True(Path.IsPathRooted(result));
        Assert.EndsWith("roms\\test", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString replaces the %BASEFOLDER% placeholder with the actual base folder.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesBaseFolderPlaceholder()
    {
        const string parameters = "-path %BASEFOLDER%\\roms";
        var result = PathHelper.ResolveParameterString(parameters);

        Assert.NotNull(result);
        Assert.DoesNotContain("%BASEFOLDER%", result, StringComparison.Ordinal);
        Assert.Contains("\\roms", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString replaces the %SYSTEMFOLDER% placeholder with the provided system folder.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesSystemFolderPlaceholder()
    {
        const string parameters = "-rompath %SYSTEMFOLDER%";
        var result = PathHelper.ResolveParameterString(parameters, ["C:\\roms\\Arcade"]);

        Assert.Equal("-rompath C:\\roms\\Arcade", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString replaces the %EMULATORFOLDER% placeholder with the provided emulator folder.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesEmulatorFolderPlaceholder()
    {
        const string parameters = "-cfg %EMULATORFOLDER%\\config.ini";
        var result = PathHelper.ResolveParameterString(parameters, null, "C:\\emulators\\mame");

        Assert.Equal("-cfg C:\\emulators\\mame\\config.ini", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString replaces multiple different placeholders in a single string.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesMultiplePlaceholders()
    {
        const string parameters = "-rompath %EMULATORFOLDER%\\roms;%SYSTEMFOLDER% -skip_gameinfo";
        var result = PathHelper.ResolveParameterString(parameters, ["C:\\roms\\Arcade"], "C:\\emulators\\mame");

        Assert.Contains("C:\\emulators\\mame\\roms", result, StringComparison.Ordinal);
        Assert.Contains("C:\\roms\\Arcade", result, StringComparison.Ordinal);
        Assert.DoesNotContain("%EMULATORFOLDER%", result, StringComparison.Ordinal);
        Assert.DoesNotContain("%SYSTEMFOLDER%", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString leaves unknown placeholders intact.
    /// </summary>
    [Fact]
    public void ResolveParameterStringKeepsUnknownPlaceholdersIntact()
    {
        const string parameters = "-path %UNKNOWN%\\test";
        var result = PathHelper.ResolveParameterString(parameters);

        Assert.Equal("-path %UNKNOWN%\\test", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString preserves game-specific placeholders like %ROMNAME% and %ROMFILE%.
    /// </summary>
    [Fact]
    public void ResolveParameterStringKeepsGameSpecificPlaceholdersIntact()
    {
        const string parameters = "-rom %ROMNAME% -name %ROMFILE%";
        var result = PathHelper.ResolveParameterString(parameters, ["C:\\roms"], "C:\\emu");

        Assert.Equal("-rom %ROMNAME% -name %ROMFILE%", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString resolves paths containing directory traversal sequences.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesPathTraversal()
    {
        const string parameters = "-path %BASEFOLDER%\\..\\..\\Windows";
        var result = PathHelper.ResolveParameterString(parameters);

        // No longer rejects path traversal; placeholder should still be resolved
        Assert.DoesNotContain("%BASEFOLDER%", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString handles quoted paths containing placeholders.
    /// </summary>
    [Fact]
    public void ResolveParameterStringHandlesQuotedPaths()
    {
        const string parameters = "-rompath \"%SYSTEMFOLDER%\"";
        var result = PathHelper.ResolveParameterString(parameters, ["C:\\My Roms\\Arcade"]);

        Assert.Equal("-rompath \"C:\\My Roms\\Arcade\"", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString resolves an exact %SYSTEMFOLDER% match correctly.
    /// </summary>
    [Fact]
    public void ResolveParameterStringExactMatchSystemFolderResolvesCorrectly()
    {
        const string parameters = "-rompath %SYSTEMFOLDER%";
        var result = PathHelper.ResolveParameterString(parameters, ["C:\\roms\\Arcade"]);

        Assert.Equal("-rompath C:\\roms\\Arcade", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString does not modify known command-line flags.
    /// </summary>
    [Fact]
    public void ResolveParameterStringKnownFlagsAreNotModified()
    {
        const string parameters = "-f -L core --fullscreen -rompath %SYSTEMFOLDER%";
        var result = PathHelper.ResolveParameterString(parameters, ["C:\\roms"]);

        Assert.Contains("-f", result, StringComparison.Ordinal);
        Assert.Contains("-L", result, StringComparison.Ordinal);
        Assert.Contains("--fullscreen", result, StringComparison.Ordinal);
        Assert.DoesNotContain("%SYSTEMFOLDER%", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders returns the full path when the file exists.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersFileExistsReturnsPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var folder = Path.Combine(baseDir, "roms");
        const string fileName = "game.zip";
        var filePath = Path.Combine(folder, fileName);

        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(filePath, "dummy");

            var systemManager = new SystemManagerService
            {
                SystemFolders = [folder]
            };

            var result = PathHelper.FindFileInSystemFolders(systemManager.SystemFolders, fileName);
            Assert.Equal(filePath, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders returns null when the file is not found.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersFileNotFoundReturnsNull()
    {
        var systemManager = new SystemManagerService
        {
            SystemFolders = ["C:\\nonexistent\\roms"]
        };

        var result = PathHelper.FindFileInSystemFolders(systemManager.SystemFolders, "missing.zip");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders resolves files stored in subfolders of a system
    ///     folder, matching the recursive scan performed by the game browser.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersFileInSubfolderReturnsPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var folder = Path.Combine(baseDir, "roms");
        var subFolder = Path.Combine(folder, "GameFolder");
        const string fileName = "disc.gdi";
        var filePath = Path.Combine(subFolder, fileName);

        try
        {
            Directory.CreateDirectory(subFolder);
            File.WriteAllText(filePath, "dummy");

            var result = PathHelper.FindFileInSystemFolders([folder], fileName);
            Assert.Equal(filePath, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders prefers a direct hit in the top-level system
    ///     folder over a same-named file in a subfolder.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersPrefersTopLevelFileOverSubfolderMatch()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var folder = Path.Combine(baseDir, "roms");
        var subFolder = Path.Combine(folder, "GameFolder");
        const string fileName = "game.zip";

        try
        {
            Directory.CreateDirectory(subFolder);
            var topLevelPath = Path.Combine(folder, fileName);
            var subFolderPath = Path.Combine(subFolder, fileName);
            File.WriteAllText(topLevelPath, "top");
            File.WriteAllText(subFolderPath, "sub");

            var result = PathHelper.FindFileInSystemFolders([folder], fileName);
            Assert.Equal(topLevelPath, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that FindFileInSystemFolders resolves a file in a subfolder even when the
    ///     stored file name differs only by case.
    /// </summary>
    [Fact]
    public void FindFileInSystemFoldersSubfolderMatchIsCaseInsensitive()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var folder = Path.Combine(baseDir, "roms");
        var subFolder = Path.Combine(folder, "GameFolder");

        try
        {
            Directory.CreateDirectory(subFolder);
            File.WriteAllText(Path.Combine(subFolder, "game.zip"), "dummy");

            var result = PathHelper.FindFileInSystemFolders([folder], "GAME.ZIP");
            Assert.Equal(Path.Combine(subFolder, "game.zip"), result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that ResolveParameterString joins multiple system folders with semicolons.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesMultipleSystemFolders()
    {
        const string parameters = "-rompath %SYSTEMFOLDER%";
        var result = PathHelper.ResolveParameterString(parameters, ["C:\\roms\\Arcade", "C:\\roms\\CPS1"]);

        Assert.Equal("-rompath C:\\roms\\Arcade;C:\\roms\\CPS1", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString replaces the %ROM% placeholder with the ROM file path.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesRomPlaceholder()
    {
        const string parameters = "-rom %ROM%";
        var result = PathHelper.ResolveParameterString(parameters, null, null, "C:\\roms\\Arcade\\game.zip");

        Assert.Equal("-rom C:\\roms\\Arcade\\game.zip", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString replaces the %NAME% placeholder with the provided name value.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesNamePlaceholder()
    {
        const string parameters = "dir=\"games/%NAME%\"";
        var result = PathHelper.ResolveParameterString(parameters, null, null, null, null, "keen4");

        Assert.Equal("dir=\"games/keen4\"", result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString correctly substitutes %NAME% with a value containing spaces.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesNamePlaceholderWithSpaces()
    {
        const string parameters = "dir=\"games/%NAME%\"";
        var result = PathHelper.ResolveParameterString(parameters, null, null, null, null, "Cosmos Cosmic Adventure 1");

        Assert.Equal("dir=\"games/Cosmos Cosmic Adventure 1\"", result);
    }

    /// <summary>
    ///     Verifies that FindContainingSystemFolder returns null when the system folders list is empty.
    /// </summary>
    [Fact]
    public void FindContainingSystemFolderEmptySystemFoldersListReturnsNull()
    {
        var systemManager = new SystemManagerService
        {
            SystemFolders = []
        };

        var result = PathHelper.FindContainingSystemFolder(systemManager.SystemFolders,
            systemManager.PrimarySystemFolder, "C:\\game.zip");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that ResolveParameterString replaces the %ROMSYSTEMFOLDER% placeholder correctly.
    /// </summary>
    [Fact]
    public void ResolveParameterStringResolvesRomSystemFolderPlaceholder()
    {
        const string parameters = "-path %ROMSYSTEMFOLDER%";
        var result = PathHelper.ResolveParameterString(parameters, null, null, null, "C:\\roms\\Arcade");

        Assert.Equal("-path C:\\roms\\Arcade", result);
    }

    /// <summary>
    ///     Verifies that TryFindFileWithNormalizedPath returns null for a null path.
    /// </summary>
    [Fact]
    public void TryFindFileWithNormalizedPathNullPathReturnsNull()
    {
        var result = PathHelper.TryFindFileWithNormalizedPath(null);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that TryFindFileWithNormalizedPath returns null for an empty path.
    /// </summary>
    [Fact]
    public void TryFindFileWithNormalizedPathEmptyPathReturnsNull()
    {
        var result = PathHelper.TryFindFileWithNormalizedPath("");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that TryFindFileWithNormalizedPath returns null for a whitespace-only path.
    /// </summary>
    [Fact]
    public void TryFindFileWithNormalizedPathWhitespacePathReturnsNull()
    {
        var result = PathHelper.TryFindFileWithNormalizedPath("   ");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that TryFindFileWithNormalizedPath returns null when the directory does not exist.
    /// </summary>
    [Fact]
    public void TryFindFileWithNormalizedPathNonExistentDirectoryReturnsNull()
    {
        var result = PathHelper.TryFindFileWithNormalizedPath("C:\\NonExistentDir\\file.txt");
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that TryFindFileWithNormalizedPath returns the original path when the file exists.
    /// </summary>
    [Fact]
    public void TryFindFileWithNormalizedPathFileExistsReturnsOriginalPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filePath = Path.Combine(baseDir, "testfile.txt");

        try
        {
            Directory.CreateDirectory(baseDir);
            File.WriteAllText(filePath, "dummy");

            var result = PathHelper.TryFindFileWithNormalizedPath(filePath);

            Assert.NotNull(result);
            Assert.Equal(filePath, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that TryFindFileWithNormalizedPath returns the directory path when it exists.
    /// </summary>
    [Fact]
    public void TryFindFileWithNormalizedPathDirectoryExistsReturnsDirectoryPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var subDir = Path.Combine(baseDir, "subdir");

        try
        {
            Directory.CreateDirectory(subDir);

            var result = PathHelper.TryFindFileWithNormalizedPath(subDir);

            Assert.NotNull(result);
            Assert.Equal(subDir, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that TryFindFileWithNormalizedPath resolves case-insensitive file name mismatches on Windows.
    /// </summary>
    [Fact]
    public void TryFindFileWithNormalizedPathCaseMismatchReturnsActualPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var actualFilePath = Path.Combine(baseDir, "TestFile.txt");
        var searchFilePath = Path.Combine(baseDir, "testfile.txt");

        try
        {
            Directory.CreateDirectory(baseDir);
            File.WriteAllText(actualFilePath, "dummy");

            var result = PathHelper.TryFindFileWithNormalizedPath(searchFilePath);

            Assert.NotNull(result);
            Assert.Equal(actualFilePath, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that ResolveActualLogFile returns the most recently written matching log file when Serilog's
    ///     daily rolling interval has date-stamped the file on disk (e.g. error_user20260825.log).
    /// </summary>
    [Fact]
    public void ResolveActualLogFileReturnsLatestRollingFile()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var preferred = Path.Combine(baseDir, "error_user.log");
        var older = Path.Combine(baseDir, "error_user20260824.log");
        var newer = Path.Combine(baseDir, "error_user20260825.log");

        try
        {
            Directory.CreateDirectory(baseDir);
            File.WriteAllText(older, "old");
            File.WriteAllText(newer, "new");
            File.SetLastWriteTimeUtc(older, new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newer, new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc));

            var result = PathHelper.ResolveActualLogFile(preferred);

            Assert.Equal(newer, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that ResolveActualLogFile falls back to the preferred base path when no matching log file
    ///     exists yet (so the caller can report that no log file has been created).
    /// </summary>
    [Fact]
    public void ResolveActualLogFileFallsBackWhenNoFileExists()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var preferred = Path.Combine(baseDir, "error_user.log");

        try
        {
            Directory.CreateDirectory(baseDir);

            var result = PathHelper.ResolveActualLogFile(preferred);

            Assert.Equal(preferred, result);
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    /// <summary>
    ///     Verifies that ResolveLogFilePath(IConfiguration) reads the 'LogPath' key and combines it with
    ///     the SimpleLauncher local app-data folder, matching the Serilog file sink location.
    /// </summary>
    [Fact]
    public void ResolveLogFilePathFromConfigurationUsesLogPathKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["LogPath"] = "custom.log"
            })
            .Build();

        var result = PathHelper.ResolveLogFilePath(configuration);

        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleLauncher",
                "custom.log"),
            result);
    }

    /// <summary>
    ///     Verifies that ResolveLogFilePath(IConfiguration) falls back to 'error_user.log' when the
    ///     'LogPath' key is missing and when the configuration is null.
    /// </summary>
    [Fact]
    public void ResolveLogFilePathFromConfigurationFallsBackToDefault()
    {
        var emptyConfiguration = new ConfigurationBuilder().Build();

        var fromEmpty = PathHelper.ResolveLogFilePath(emptyConfiguration);
        var fromNull = PathHelper.ResolveLogFilePath((IConfiguration?)null);

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleLauncher",
            "error_user.log");
        Assert.Equal(expected, fromEmpty);
        Assert.Equal(expected, fromNull);
    }
}