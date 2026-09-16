using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Services;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the <see cref="DataFileLocation" /> class.
/// </summary>
public class DataFileLocationTests
{
    /// <summary>
    ///     Verifies that the constructor sets the file name to end with the specified name.
    /// </summary>
    [Fact]
    public void ConstructorSetsFileName()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location = new DataFileLocation(uniqueName);

        Assert.NotNull(location.FilePath);
        Assert.EndsWith(uniqueName, location.FilePath, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that TempFilePath appends a .tmp extension to the FilePath.
    /// </summary>
    [Fact]
    public void TempFilePathAppendsTmpExtension()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location = new DataFileLocation(uniqueName);

        Assert.Equal(location.FilePath + ".tmp", location.TempFilePath);
    }

    /// <summary>
    ///     Verifies that FilePath is not empty after construction.
    /// </summary>
    [Fact]
    public void FilePathIsNotEmpty()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location = new DataFileLocation(uniqueName);

        Assert.NotEmpty(location.FilePath);
    }

    /// <summary>
    ///     Verifies that IsPortableMode returns a valid boolean value.
    /// </summary>
    [Fact]
    public void IsPortableModeIsSet()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location = new DataFileLocation(uniqueName);

        // IsPortableMode should be a valid boolean (no exception)
        _ = location.IsPortableMode;
    }

    /// <summary>
    ///     Verifies that GetLocalAppDataPath returns a valid path ending with the file name.
    /// </summary>
    [Fact]
    public void GetLocalAppDataPathReturnsValidPath()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location = new DataFileLocation(uniqueName);

        var localPath = location.GetLocalAppDataPath();

        Assert.NotNull(localPath);
        Assert.EndsWith(uniqueName, localPath, StringComparison.Ordinal);
        Assert.Contains("SimpleLauncher", localPath, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that GetLocalAppDataPath starts with the LocalApplicationData folder path.
    /// </summary>
    [Fact]
    public void GetLocalAppDataPathContainsLocalAppData()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location = new DataFileLocation(uniqueName);

        var localPath = location.GetLocalAppDataPath();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(localAppData, localPath, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that TryFallbackToLocalAppData returns true and updates the FilePath to the local app data location.
    /// </summary>
    [Fact]
    public void TryFallbackToLocalAppDataReturnsTrueAndUpdatesState()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location = new DataFileLocation(uniqueName);

        var result = location.TryFallbackToLocalAppData();

        Assert.True(result);
        Assert.False(location.IsPortableMode);
        Assert.Contains("SimpleLauncher", location.FilePath, StringComparison.Ordinal);
        Assert.EndsWith(uniqueName, location.FilePath, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that TryFallbackToLocalAppData sets the FilePath to match GetLocalAppDataPath.
    /// </summary>
    [Fact]
    public void TryFallbackToLocalAppDataSetsCorrectPath()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location = new DataFileLocation(uniqueName);

        location.TryFallbackToLocalAppData();

        var expectedPath = location.GetLocalAppDataPath();
        Assert.Equal(expectedPath, location.FilePath);
    }

    /// <summary>
    ///     Verifies that multiple instances with the same file name produce the same local app data path.
    /// </summary>
    [Fact]
    public void MultipleInstancesWithSameFileNameHaveSameLocalPath()
    {
        var uniqueName = $"testfile_{Guid.NewGuid():N}.xml";
        var location1 = new DataFileLocation(uniqueName);
        var location2 = new DataFileLocation(uniqueName);

        Assert.Equal(location1.GetLocalAppDataPath(), location2.GetLocalAppDataPath());
    }

    /// <summary>
    ///     Verifies that a configured path containing a folder (%BASEFOLDER%\system.xml) yields a
    ///     bare file name for the LocalAppData fallback instead of creating a literal
    ///     '%BASEFOLDER%' folder there. The Avalonia SystemManagerService used to pass the
    ///     configured path as the default file name.
    /// </summary>
    [Fact]
    public void ConfiguredFolderPathUsesBareFileNameForLocalAppDataPath()
    {
        var uniqueName = $"sl_fileloc_{Guid.NewGuid():N}.xml";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SystemXmlPath"] = $"%BASEFOLDER%\\{uniqueName}"
            })
            .Build();

        var location = new DataFileLocation(configuration, "SystemXmlPath", $"%BASEFOLDER%\\{uniqueName}");
        var localPath = location.GetLocalAppDataPath();

        Assert.Equal(uniqueName, Path.GetFileName(localPath));
        Assert.DoesNotContain("%BASEFOLDER%", localPath, StringComparison.OrdinalIgnoreCase);
    }
}