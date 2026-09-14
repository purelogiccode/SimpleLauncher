using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for game file finder utilities (FindDefaultXex, FindDefaultXbe, FindCueFile, FindBinFile, FindImageIso,
///     FindEbootBin).
/// </summary>
public class FindGameFileTests : IDisposable
{
    private readonly ILogger _logErrors = new NoOpLogger();
    private readonly ILogger _logger = Log.Logger;
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FindGameFileTests" /> class, setting up a temporary test directory.
    /// </summary>
    public FindGameFileTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_FindGame_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the temporary test directory.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    // FindDefaultXex tests
    /// <summary>
    ///     Verifies that FindDefaultXex returns null when the path is null.
    /// </summary>
    [Fact]
    public void FindDefaultXexNullPathReturnsNull()
    {
        var result = FindDefaultXex.Find(null!, _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindDefaultXex returns null when the path is empty.
    /// </summary>
    [Fact]
    public void FindDefaultXexEmptyPathReturnsNull()
    {
        var result = FindDefaultXex.Find("", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindDefaultXex returns null when the directory does not exist.
    /// </summary>
    [Fact]
    public void FindDefaultXexNonExistentDirectoryReturnsNull()
    {
        var result = FindDefaultXex.Find(@"C:\nonexistent\dir", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindDefaultXex returns the file path when default.xex exists.
    /// </summary>
    [Fact]
    public void FindDefaultXexFileExistsReturnsPath()
    {
        var xexPath = Path.Combine(_testDirectory, "default.xex");
        File.WriteAllText(xexPath, "fake");

        var result = FindDefaultXex.Find(_testDirectory, _logErrors);

        Assert.NotNull(result);
        Assert.Equal(xexPath, result);
    }

    /// <summary>
    ///     Verifies that FindDefaultXex returns null when default.xex does not exist in the directory.
    /// </summary>
    [Fact]
    public void FindDefaultXexFileDoesNotExistReturnsNull()
    {
        var result = FindDefaultXex.Find(_testDirectory, _logErrors);
        Assert.Null(result);
    }

    // FindDefaultXbe tests
    /// <summary>
    ///     Verifies that FindDefaultXbe returns null when the path is null.
    /// </summary>
    [Fact]
    public void FindDefaultXbeNullPathReturnsNull()
    {
        var result = FindDefaultXbe.Find(null!, _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindDefaultXbe returns null when the path is empty.
    /// </summary>
    [Fact]
    public void FindDefaultXbeEmptyPathReturnsNull()
    {
        var result = FindDefaultXbe.Find("", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindDefaultXbe returns null when the directory does not exist.
    /// </summary>
    [Fact]
    public void FindDefaultXbeNonExistentDirectoryReturnsNull()
    {
        var result = FindDefaultXbe.Find(@"C:\nonexistent\dir", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindDefaultXbe returns the file path when default.xbe exists.
    /// </summary>
    [Fact]
    public void FindDefaultXbeFileExistsReturnsPath()
    {
        var xbePath = Path.Combine(_testDirectory, "default.xbe");
        File.WriteAllText(xbePath, "fake");

        var result = FindDefaultXbe.Find(_testDirectory, _logErrors);

        Assert.NotNull(result);
        Assert.Equal(xbePath, result);
    }

    /// <summary>
    ///     Verifies that FindDefaultXbe returns null when default.xbe does not exist in the directory.
    /// </summary>
    [Fact]
    public void FindDefaultXbeFileDoesNotExistReturnsNull()
    {
        var result = FindDefaultXbe.Find(_testDirectory, _logErrors);
        Assert.Null(result);
    }

    // FindCueFile tests
    /// <summary>
    ///     Verifies that FindCueFile returns null when the path is null.
    /// </summary>
    [Fact]
    public void FindCueFileNullPathReturnsNull()
    {
        var result = FindCueFile.Find(null!, _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindCueFile returns null when the path is empty.
    /// </summary>
    [Fact]
    public void FindCueFileEmptyPathReturnsNull()
    {
        var result = FindCueFile.Find("", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindCueFile returns null when the directory does not exist.
    /// </summary>
    [Fact]
    public void FindCueFileNonExistentDirectoryReturnsNull()
    {
        var result = FindCueFile.Find(@"C:\nonexistent\dir", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindCueFile returns the file path when a .cue file exists.
    /// </summary>
    [Fact]
    public void FindCueFileFileExistsReturnsPath()
    {
        var cuePath = Path.Combine(_testDirectory, "game.cue");
        File.WriteAllText(cuePath, "fake");

        var result = FindCueFile.Find(_testDirectory, _logErrors);

        Assert.NotNull(result);
        Assert.Equal(cuePath, result);
    }

    /// <summary>
    ///     Verifies that FindCueFile returns one of the .cue files when multiple exist.
    /// </summary>
    [Fact]
    public void FindCueFileMultipleCueFilesReturnsFirst()
    {
        var cue1 = Path.Combine(_testDirectory, "disc1.cue");
        var cue2 = Path.Combine(_testDirectory, "disc2.cue");
        File.WriteAllText(cue1, "fake");
        File.WriteAllText(cue2, "fake");

        var result = FindCueFile.Find(_testDirectory, _logErrors);

        Assert.NotNull(result);
        Assert.True(string.Equals(result, cue1, StringComparison.Ordinal) ||
                    string.Equals(result, cue2, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Verifies that FindCueFile returns null when no .cue files exist in the directory.
    /// </summary>
    [Fact]
    public void FindCueFileNoCueFilesReturnsNull()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.bin"), "fake");

        var result = FindCueFile.Find(_testDirectory, _logErrors);
        Assert.Null(result);
    }

    // FindBinFile tests
    /// <summary>
    ///     Verifies that FindBinFile returns null when the path is null.
    /// </summary>
    [Fact]
    public void FindBinFileNullPathReturnsNull()
    {
        var result = FindBinFile.Find(null!, _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindBinFile returns null when the path is empty.
    /// </summary>
    [Fact]
    public void FindBinFileEmptyPathReturnsNull()
    {
        var result = FindBinFile.Find("", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindBinFile returns null when the directory does not exist.
    /// </summary>
    [Fact]
    public void FindBinFileNonExistentDirectoryReturnsNull()
    {
        var result = FindBinFile.Find(@"C:\nonexistent\dir", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindBinFile returns the file path when a .bin file exists.
    /// </summary>
    [Fact]
    public void FindBinFileFileExistsReturnsPath()
    {
        var binPath = Path.Combine(_testDirectory, "game.bin");
        File.WriteAllText(binPath, "fake");

        var result = FindBinFile.Find(_testDirectory, _logErrors);

        Assert.NotNull(result);
        Assert.Equal(binPath, result);
    }

    /// <summary>
    ///     Verifies that FindBinFile returns null when no .bin files exist in the directory.
    /// </summary>
    [Fact]
    public void FindBinFileNoBinFilesReturnsNull()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "game.cue"), "fake");

        var result = FindBinFile.Find(_testDirectory, _logErrors);
        Assert.Null(result);
    }

    // FindImageIso tests
    /// <summary>
    ///     Verifies that FindImageIso returns null when the path is null.
    /// </summary>
    [Fact]
    public void FindImageIsoNullPathReturnsNull()
    {
        var result = FindImageIso.Find(null!, _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindImageIso returns null when the path is empty.
    /// </summary>
    [Fact]
    public void FindImageIsoEmptyPathReturnsNull()
    {
        var result = FindImageIso.Find("", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindImageIso returns null when the directory does not exist.
    /// </summary>
    [Fact]
    public void FindImageIsoNonExistentDirectoryReturnsNull()
    {
        var result = FindImageIso.Find(@"C:\nonexistent\dir", _logErrors);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindImageIso returns the file path when an .iso file exists.
    /// </summary>
    [Fact]
    public void FindImageIsoFileExistsReturnsPath()
    {
        var isoPath = Path.Combine(_testDirectory, "image.iso");
        File.WriteAllText(isoPath, "fake");

        var result = FindImageIso.Find(_testDirectory, _logErrors);

        Assert.NotNull(result);
        Assert.Equal(isoPath, result);
    }

    /// <summary>
    ///     Verifies that FindImageIso returns null when no .iso file exists in the directory.
    /// </summary>
    [Fact]
    public void FindImageIsoFileDoesNotExistReturnsNull()
    {
        var result = FindImageIso.Find(_testDirectory, _logErrors);
        Assert.Null(result);
    }

    // FindEbootBin tests
    /// <summary>
    ///     Verifies that FindEbootBin returns null when the path is null.
    /// </summary>
    [Fact]
    public void FindEbootBinNullPathReturnsNull()
    {
        var result = FindEbootBin.FindEbootBinRecursive(null!, _logErrors, _logger);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin returns null when the path is empty.
    /// </summary>
    [Fact]
    public void FindEbootBinEmptyPathReturnsNull()
    {
        var result = FindEbootBin.FindEbootBinRecursive("", _logErrors, _logger);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin finds EBOOT.BIN in the top-level directory.
    /// </summary>
    [Fact]
    public void FindEbootBinInTopDirectoryReturnsPath()
    {
        var ebootPath = Path.Combine(_testDirectory, "EBOOT.BIN");
        File.WriteAllText(ebootPath, "fake");

        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);

        Assert.NotNull(result);
        Assert.Equal(ebootPath, result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin finds EBOOT.BIN in the PS3_GAME/USRDIR directory structure.
    /// </summary>
    [Fact]
    public void FindEbootBinInPs3GameUsrDirReturnsPath()
    {
        var ps3GameDir = Path.Combine(_testDirectory, "PS3_GAME");
        var usrDir = Path.Combine(ps3GameDir, "USRDIR");
        Directory.CreateDirectory(usrDir);
        var ebootPath = Path.Combine(usrDir, "EBOOT.BIN");
        File.WriteAllText(ebootPath, "fake");

        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);

        Assert.NotNull(result);
        Assert.Equal(ebootPath, result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin finds EBOOT.BIN in a deeply nested directory.
    /// </summary>
    [Fact]
    public void FindEbootBinInNestedDirectoryReturnsPath()
    {
        var nestedDir = Path.Combine(_testDirectory, "subdir", "deep");
        Directory.CreateDirectory(nestedDir);
        var ebootPath = Path.Combine(nestedDir, "EBOOT.BIN");
        File.WriteAllText(ebootPath, "fake");

        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);

        Assert.NotNull(result);
        Assert.Equal(ebootPath, result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin returns null when no EBOOT.BIN file exists.
    /// </summary>
    [Fact]
    public void FindEbootBinNotFoundReturnsNull()
    {
        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);
        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin prefers the top-level directory over nested subdirectories.
    /// </summary>
    [Fact]
    public void FindEbootBinPrefersTopDirectoryOverNested()
    {
        var topEboot = Path.Combine(_testDirectory, "EBOOT.BIN");
        File.WriteAllText(topEboot, "top");

        var nestedDir = Path.Combine(_testDirectory, "PS3_GAME", "USRDIR");
        Directory.CreateDirectory(nestedDir);
        File.WriteAllText(Path.Combine(nestedDir, "EBOOT.BIN"), "nested");

        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);

        Assert.NotNull(result);
        Assert.Equal(topEboot, result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin prefers the PS3_GAME/USRDIR structure over full recursive search.
    /// </summary>
    [Fact]
    public void FindEbootBinPrefersPs3StructureOverFullRecursive()
    {
        var ps3GameDir = Path.Combine(_testDirectory, "PS3_GAME");
        var usrDir = Path.Combine(ps3GameDir, "USRDIR");
        Directory.CreateDirectory(usrDir);
        var ps3Eboot = Path.Combine(usrDir, "EBOOT.BIN");
        File.WriteAllText(ps3Eboot, "ps3");

        var deepDir = Path.Combine(_testDirectory, "other", "deep");
        Directory.CreateDirectory(deepDir);
        File.WriteAllText(Path.Combine(deepDir, "EBOOT.BIN"), "deep");

        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);

        Assert.NotNull(result);
        Assert.Equal(ps3Eboot, result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin matches the file name case-insensitively (required on
    ///     case-sensitive filesystems and on some mounted virtual drives).
    /// </summary>
    [Fact]
    public void FindEbootBinFindsLowerCaseName()
    {
        var nestedDir = Path.Combine(_testDirectory, "PS3_GAME", "USRDIR");
        Directory.CreateDirectory(nestedDir);
        var ebootPath = Path.Combine(nestedDir, "eboot.bin");
        File.WriteAllText(ebootPath, "fake");

        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);

        Assert.NotNull(result);
        Assert.Equal(ebootPath, result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin matches the PS3_GAME/USRDIR directory names case-insensitively.
    /// </summary>
    [Fact]
    public void FindEbootBinFindsLowerCasePs3Structure()
    {
        var nestedDir = Path.Combine(_testDirectory, "ps3_game", "usrdir");
        Directory.CreateDirectory(nestedDir);
        var ebootPath = Path.Combine(nestedDir, "EBOOT.BIN");
        File.WriteAllText(ebootPath, "fake");

        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);

        Assert.NotNull(result);
        Assert.Equal(ebootPath, result);
    }

    /// <summary>
    ///     Verifies that FindEbootBin finds EBOOT.BIN even when the entry is flagged hidden/system,
    ///     as some mounted filesystems report unusual attributes for disc entries.
    /// </summary>
    [Fact]
    public void FindEbootBinFindsHiddenFile()
    {
        var ebootPath = Path.Combine(_testDirectory, "EBOOT.BIN");
        File.WriteAllText(ebootPath, "fake");
        File.SetAttributes(ebootPath, FileAttributes.Hidden | FileAttributes.System);

        var result = FindEbootBin.FindEbootBinRecursive(_testDirectory, _logErrors, _logger);

        Assert.NotNull(result);
        Assert.Equal(ebootPath, result);
    }
}