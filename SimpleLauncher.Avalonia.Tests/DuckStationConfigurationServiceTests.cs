using SimpleLauncher.Core.Services.InjectEmulatorConfig;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Verifies that an unwritable DuckStation configuration path is surfaced as a classified
///     permission condition, so the Avalonia launch pipeline can keep launching the game with the
///     emulator's default settings instead of filing a bug report (bugs 70528/70547).
/// </summary>
public class DuckStationConfigurationServiceTests : IDisposable
{
    private readonly string _testDirectory;

    /// <summary>
    ///     Creates an isolated emulator directory for the configuration-injection test.
    /// </summary>
    public DuckStationConfigurationServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_DuckStationTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Removes the isolated emulator directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
    }

    /// <summary>
    ///     A directory named <c>settings.ini</c> makes the sample copy fail; the service must wrap
    ///     the raw I/O failure in <see cref="DuckStationPermissionException" />.
    /// </summary>
    [Fact]
    public void BlockedConfigPathThrowsPermissionException()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "portable.txt"), string.Empty);
        Directory.CreateDirectory(Path.Combine(_testDirectory, "settings.ini"));

        var settings = TestDependencies.Settings();

        Assert.Throws<DuckStationPermissionException>(() =>
            DuckStationConfigurationService.InjectSettings(
                Path.Combine(_testDirectory, "duckstation-qt-x64-ReleaseLTCG.exe"),
                settings,
                TestDependencies.Logger().Object));
    }
}
