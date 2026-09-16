using Xunit;
using ValidateBatchFile = SimpleLauncher.Core.Services.GameLauncher.ValidateBatchFile;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests batch file validation logic that detects missing file paths and invalid quoted paths in batch scripts.
/// </summary>
public class ValidateBatchFileTests : IDisposable
{
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidateBatchFileTests" /> class,
    ///     creating a temporary test directory.
    /// </summary>
    public ValidateBatchFileTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_BatchTest_{Guid.NewGuid():N}");
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

    /// <summary>
    ///     Verifies that validating a non-existent file path returns an empty list.
    /// </summary>
    [Fact]
    public void ValidateBatchFileContentsNonExistentFileReturnsEmptyList()
    {
        var result = ValidateBatchFile.ValidateBatchFileContents(@"C:\nonexistent\file.bat");
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that validating an empty batch file returns an empty list.
    /// </summary>
    [Fact]
    public void ValidateBatchFileContentsEmptyFileReturnsEmptyList()
    {
        var batchFile = Path.Combine(_testDirectory, "empty.bat");
        File.WriteAllText(batchFile, "");

        var result = ValidateBatchFile.ValidateBatchFileContents(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that a batch file with only echo commands returns no missing paths.
    /// </summary>
    [Fact]
    public void ValidateBatchFileContentsValidPathsReturnsEmptyList()
    {
        var batchFile = Path.Combine(_testDirectory, "valid.bat");
        File.WriteAllText(batchFile, "@echo off\necho hello\n");

        var result = ValidateBatchFile.ValidateBatchFileContents(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that paths to non-existent executables are detected as missing.
    /// </summary>
    [Fact]
    public void ValidateBatchFileContentsMissingPathsReturnsMissingPaths()
    {
        var batchFile = Path.Combine(_testDirectory, "missing.bat");
        File.WriteAllText(batchFile, "@echo off\n\"C:\\nonexistent\\game.exe\" --arg\n");

        var result = ValidateBatchFile.ValidateBatchFileContents(batchFile);
        Assert.Contains(result, static p => p.Contains("game.exe", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Verifies that lines starting with rem or :: (comments) are skipped during validation.
    /// </summary>
    [Fact]
    public void ValidateBatchFileContentsRemLinesAreSkipped()
    {
        var batchFile = Path.Combine(_testDirectory, "rem.bat");
        File.WriteAllText(batchFile, "rem \"C:\\nonexistent\\file.exe\"\n:: also a comment\n");

        var result = ValidateBatchFile.ValidateBatchFileContents(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that lines starting with # are treated as comments and skipped.
    /// </summary>
    [Fact]
    public void ValidateBatchFileContentsCommentsWithHashAreSkipped()
    {
        var batchFile = Path.Combine(_testDirectory, "comment.bat");
        File.WriteAllText(batchFile, "# \"C:\\nonexistent\\file.exe\"\n");

        var result = ValidateBatchFile.ValidateBatchFileContents(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that <see cref="ValidateBatchFile.FindInvalidQuotedPathsSimple" /> returns empty for a non-existent file.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleNonExistentFileReturnsEmptyList()
    {
        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(@"C:\nonexistent\file.bat");
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that FindInvalidQuotedPathsSimple skips rem/:: comments like
    ///     ValidateBatchFileContents does: a commented-out path must not trigger the
    ///     false "some paths are missing, continue?" prompt at launch.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleCommentLinesAreSkipped()
    {
        var batchFile = Path.Combine(_testDirectory, "comments.bat");
        File.WriteAllText(batchFile, "rem \"C:\\nonexistent\\old.exe\"\n:: \"C:\\nonexistent\\also_old.exe\"\n");

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that <see cref="ValidateBatchFile.FindInvalidQuotedPathsSimple" /> returns empty for an empty file.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleEmptyFileReturnsEmptyList()
    {
        var batchFile = Path.Combine(_testDirectory, "empty2.bat");
        File.WriteAllText(batchFile, "");

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that a quoted path with a drive letter pointing to a non-existent location is detected.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleMissingDrivePathReturnsPath()
    {
        var batchFile = Path.Combine(_testDirectory, "drive.bat");
        File.WriteAllText(batchFile, "\"C:\\nonexistent\\path\\file.exe\"\n");

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.Single(result);
        Assert.Contains("file.exe", result[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that a quoted path to an existing directory is not flagged as invalid.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleValidExistingPathReturnsEmpty()
    {
        var existingDir = Path.Combine(_testDirectory, "existing");
        Directory.CreateDirectory(existingDir);

        var batchFile = Path.Combine(_testDirectory, "valid.bat");
        File.WriteAllText(batchFile, $"\"{existingDir}\"\n");

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that a UNC path is detected and validated as a file path.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleUncPathIsDetectedAsPath()
    {
        var batchFile = Path.Combine(_testDirectory, "unc.bat");
        File.WriteAllText(batchFile, "\"\\\\server\\share\\file.exe\"\n");

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.Single(result);
    }

    /// <summary>
    ///     Verifies that quoted text that is not a file path is ignored.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleNonPathTextIsIgnored()
    {
        var batchFile = Path.Combine(_testDirectory, "nonpath.bat");
        File.WriteAllText(batchFile, "\"just some text\"\n");

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that multiple invalid quoted paths on the same line are all detected.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleMultipleQuotedPathsFindsAll()
    {
        var batchFile = Path.Combine(_testDirectory, "multi.bat");
        File.WriteAllText(batchFile, "\"C:\\missing1\\a.exe\" \"D:\\missing2\\b.exe\"\n");

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.Equal(2, result.Count);
    }

    /// <summary>
    ///     Verifies that an existing executable referenced in a batch file is not reported as missing.
    /// </summary>
    [Fact]
    public void ValidateBatchFileContentsExistingFileNotReported()
    {
        var batchFile = Path.Combine(_testDirectory, "existing.bat");
        var testExe = Path.Combine(_testDirectory, "existing.exe");
        File.WriteAllText(testExe, "test");
        File.WriteAllText(batchFile, $"start \"{testExe}\"");

        var result = ValidateBatchFile.ValidateBatchFileContents(batchFile);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that FindInvalidQuotedPathsSimple detects missing paths in batch files.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleDetectsMissingPaths()
    {
        var batchFile = Path.Combine(_testDirectory, "missing.bat");
        File.WriteAllText(batchFile, "\"C:\\nonexistent_folder_67890\\tool.exe\"\n");

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.NotEmpty(result);
        Assert.Contains("tool.exe", result[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that FindInvalidQuotedPathsSimple skips non-path quoted strings.
    /// </summary>
    [Fact]
    public void FindInvalidQuotedPathsSimpleSkipNonPaths()
    {
        var batchFile = Path.Combine(_testDirectory, "nonpath.bat");
        File.WriteAllText(batchFile, """
                                     @echo off
                                     set "NAME=hello"
                                     echo "this is not a path"
                                     """);

        var result = ValidateBatchFile.FindInvalidQuotedPathsSimple(batchFile);
        Assert.Empty(result);
    }
}