using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UnifiedSettings;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Bug #67097: a failed background settings save faulted the fire-and-forget task
///     and surfaced later as an unobserved task exception (SQLite 'attempt to write a
///     readonly database'). SaveAsync must observe and log the failure instead.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public sealed class SettingsManagerSaveFailureTests : IDisposable
{
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), $"SL_SaveFailure_{Guid.NewGuid():N}");

    /// <summary>Creates the isolated temporary directory used by each test.</summary>
    public SettingsManagerSaveFailureTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task SaveAsync_WhenDatabaseCannotBeWritten_DoesNotThrow()
    {
        // Point the unified database below a regular file, so creating its parent
        // "folder" (and therefore every save) fails deterministically.
        var blocker = Path.Combine(_testDirectory, "blocker");
        File.WriteAllText(blocker, "not a directory");
        UnifiedSettingsDatabase.DatabasePathOverride = Path.Combine(blocker, "settings.dat");

        try
        {
            using var settings = new SettingsManagerService(
                _configuration, new NoOpLogger(), new NoOpCredentialProtector(), null, true);
            settings.ThumbnailSize = 300;

            await settings.SaveAsync(); // must complete without throwing
        }
        finally
        {
            UnifiedTestDatabase.ClearRedirect();
        }
    }

    /// <summary>Clears the database redirection and removes the temporary directory.</summary>
    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}