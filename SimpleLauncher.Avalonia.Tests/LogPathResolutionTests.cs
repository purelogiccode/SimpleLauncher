using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.CheckPaths;
using SimpleLauncher.Diagnostics;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Verifies that every diagnostics dialog resolves the same <c>error_user.log</c> location the
///     Avalonia Serilog sink writes to, and that the real rolling log is preferred over the base
///     file used by the bug-report sink (bug 70618).
/// </summary>
public class LogPathResolutionTests : IDisposable
{
    private readonly string _testDirectory;

    /// <summary>
    ///     Creates an isolated directory for rolling-log resolution tests.
    /// </summary>
    public LogPathResolutionTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_LogPathTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Removes the isolated test directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
    }

    /// <summary>
    ///     The configured LogPath must resolve inside the shared SimpleLauncher app-data folder,
    ///     which is also what <c>App.axaml.cs</c> passes to the Serilog file sink.
    /// </summary>
    [Fact]
    public void ResolveLogFilePath_UsesConfiguredNameInAppDataFolder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["LogPath"] = "custom.log"
            })
            .Build();

        Assert.Equal(
            Path.Combine(AppDataPaths.SimpleLauncherDataFolder, "custom.log"),
            PathHelper.ResolveLogFilePath(configuration));
    }

    /// <summary>
    ///     A directory component in LogPath must not redirect the log outside the app-data folder.
    /// </summary>
    [Fact]
    public void ResolveLogFilePath_StripsDirectoryComponents()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["LogPath"] = Path.Combine("..", "..", "hijacked.log")
            })
            .Build();

        Assert.Equal(
            Path.Combine(AppDataPaths.SimpleLauncherDataFolder, "hijacked.log"),
            PathHelper.ResolveLogFilePath(configuration));
    }

    /// <summary>
    ///     When both the base file and a date-stamped rolling file exist, the rolling file must win
    ///     even if the base file was written more recently by the bug-report sink.
    /// </summary>
    [Fact]
    public void ResolveActualLogFile_PrefersRollingFileOverBaseFile()
    {
        var preferred = Path.Combine(_testDirectory, "error_user.log");
        var rolling = Path.Combine(_testDirectory, "error_user20260101.log");

        File.WriteAllText(rolling, "rolling");
        File.WriteAllText(preferred, "report dump");
        File.SetLastWriteTimeUtc(rolling, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(preferred, new DateTime(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(rolling, PathHelper.ResolveActualLogFile(preferred));
    }

    /// <summary>
    ///     When only the base file exists (bug-report sink wrote it before Serilog rolled a file),
    ///     it is still opened instead of reporting a missing log.
    /// </summary>
    [Fact]
    public void ResolveActualLogFile_FallsBackToBaseFile()
    {
        var preferred = Path.Combine(_testDirectory, "error_user.log");
        File.WriteAllText(preferred, "report dump");

        Assert.Equal(preferred, PathHelper.ResolveActualLogFile(preferred));
    }

    /// <summary>
    ///     When nothing exists yet, the preferred path is returned so the dialog can report that
    ///     no log file has been created.
    /// </summary>
    [Fact]
    public void ResolveActualLogFile_ReturnsPreferredWhenNoFileExists()
    {
        var preferred = Path.Combine(_testDirectory, "error_user.log");

        Assert.Equal(preferred, PathHelper.ResolveActualLogFile(preferred));
    }
}
