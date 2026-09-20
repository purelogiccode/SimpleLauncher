using SimpleLauncher.Core.Services.UnifiedSettings;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Cross-platform tests for the portable vs per-user database location resolution
///     (<see cref="UnifiedSettingsDatabase.ResolveDatabasePath" />): Windows keeps portable mode
///     for a writable exe folder, Linux/macOS default to <c>~/.local/share/SimpleLauncher</c>,
///     and an existing database always wins so migrations keep working.
/// </summary>
public class UnifiedSettingsDatabasePathTests
{
    private static string FileName => UnifiedSettingsDatabase.DatabaseFileName;

    [Fact]
    public void ResolveDatabasePath_NoDatabase_WindowsWritableFolder_UsesPortableFolder()
    {
        var baseDir = Directory.CreateTempSubdirectory("sl-dbpath-");
        try
        {
            var appDataDir = Path.Combine(baseDir.FullName, "appdata");

            var result = UnifiedSettingsDatabase.ResolveDatabasePath(baseDir.FullName, appDataDir, true);

            Assert.Equal(Path.Combine(baseDir.FullName, FileName), result);
        }
        finally
        {
            baseDir.Delete(true);
        }
    }

    [Fact]
    public void ResolveDatabasePath_NoDatabase_Unix_UsesAppDataFolder()
    {
        var baseDir = Directory.CreateTempSubdirectory("sl-dbpath-");
        try
        {
            var appDataDir = Path.Combine(baseDir.FullName, "appdata");

            var result = UnifiedSettingsDatabase.ResolveDatabasePath(baseDir.FullName, appDataDir, false);

            Assert.Equal(Path.Combine(appDataDir, FileName), result);
        }
        finally
        {
            baseDir.Delete(true);
        }
    }

    [Fact]
    public void ResolveDatabasePath_NoDatabase_NonWritableWindowsFolder_UsesAppDataFolder()
    {
        var rootDir = Directory.CreateTempSubdirectory("sl-dbpath-");
        try
        {
            var baseDir = Path.Combine(rootDir.FullName, "missing");
            var appDataDir = Path.Combine(rootDir.FullName, "appdata");

            var result = UnifiedSettingsDatabase.ResolveDatabasePath(baseDir, appDataDir, true);

            Assert.Equal(Path.Combine(appDataDir, FileName), result);
        }
        finally
        {
            rootDir.Delete(true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveDatabasePath_ExistingPortableDatabase_IsKept(bool isWindows)
    {
        var rootDir = Directory.CreateTempSubdirectory("sl-dbpath-");
        try
        {
            var baseDir = Path.Combine(rootDir.FullName, "app");
            Directory.CreateDirectory(baseDir);
            var portablePath = Path.Combine(baseDir, FileName);
            File.WriteAllText(portablePath, "");
            var appDataDir = Path.Combine(rootDir.FullName, "appdata");

            var result = UnifiedSettingsDatabase.ResolveDatabasePath(baseDir, appDataDir, isWindows);

            Assert.Equal(portablePath, result);
        }
        finally
        {
            rootDir.Delete(true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveDatabasePath_ExistingAppDataDatabase_IsUsed(bool isWindows)
    {
        var rootDir = Directory.CreateTempSubdirectory("sl-dbpath-");
        try
        {
            var baseDir = Path.Combine(rootDir.FullName, "app");
            var appDataDir = Path.Combine(rootDir.FullName, "appdata");
            Directory.CreateDirectory(appDataDir);
            var appDataPath = Path.Combine(appDataDir, FileName);
            File.WriteAllText(appDataPath, "");

            var result = UnifiedSettingsDatabase.ResolveDatabasePath(baseDir, appDataDir, isWindows);

            Assert.Equal(appDataPath, result);
        }
        finally
        {
            rootDir.Delete(true);
        }
    }

    [Fact]
    public void ResolveDatabasePath_NewestOfBothDatabasesWins()
    {
        var rootDir = Directory.CreateTempSubdirectory("sl-dbpath-");
        try
        {
            var baseDir = Path.Combine(rootDir.FullName, "app");
            var appDataDir = Path.Combine(rootDir.FullName, "appdata");
            Directory.CreateDirectory(baseDir);
            Directory.CreateDirectory(appDataDir);
            var portablePath = Path.Combine(baseDir, FileName);
            var appDataPath = Path.Combine(appDataDir, FileName);
            File.WriteAllText(portablePath, "");
            File.WriteAllText(appDataPath, "");

            File.SetLastWriteTimeUtc(portablePath, DateTime.UtcNow.AddMinutes(-5));
            File.SetLastWriteTimeUtc(appDataPath, DateTime.UtcNow);
            Assert.Equal(appDataPath, UnifiedSettingsDatabase.ResolveDatabasePath(baseDir, appDataDir, true));
            Assert.Equal(appDataPath, UnifiedSettingsDatabase.ResolveDatabasePath(baseDir, appDataDir, false));

            File.SetLastWriteTimeUtc(portablePath, DateTime.UtcNow.AddMinutes(5));
            Assert.Equal(portablePath, UnifiedSettingsDatabase.ResolveDatabasePath(baseDir, appDataDir, true));
            Assert.Equal(portablePath, UnifiedSettingsDatabase.ResolveDatabasePath(baseDir, appDataDir, false));
        }
        finally
        {
            rootDir.Delete(true);
        }
    }
}
