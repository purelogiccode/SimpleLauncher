using Microsoft.Extensions.DependencyInjection;
using Moq;
using SimpleLauncher.Avalonia.Services.GameLauncher.Handlers;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Verifies the launch-pipeline contract for DuckStation configuration failures: a blocked
///     Documents\DuckStation folder must show guidance and still allow the game to launch with the
///     emulator's default settings (bugs 70528/70547).
/// </summary>
public class DuckStationConfigHandlerTests : IDisposable
{
    private readonly string _emulatorDirectory;

    /// <summary>
    ///     Creates an isolated emulator directory.
    /// </summary>
    public DuckStationConfigHandlerTests()
    {
        _emulatorDirectory = Path.Combine(Path.GetTempPath(), $"SL_DuckStationHandler_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_emulatorDirectory);
    }

    /// <summary>
    ///     Removes the isolated emulator directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_emulatorDirectory)) Directory.Delete(_emulatorDirectory, true);
    }

    /// <summary>
    ///     A permission failure during automatic config injection must not abort the launch.
    /// </summary>
    [Fact]
    public async Task PermissionFailureShowsGuidanceAndAllowsLaunch()
    {
        var emulatorPath = Path.Combine(_emulatorDirectory, "duckstation-qt-x64-ReleaseLTCG.exe");
        File.WriteAllText(emulatorPath, string.Empty);
        File.WriteAllText(Path.Combine(_emulatorDirectory, "portable.txt"), string.Empty);

        // A directory named settings.ini makes the sample copy fail; the service wraps it in
        // DuckStationPermissionException.
        Directory.CreateDirectory(Path.Combine(_emulatorDirectory, "settings.ini"));

        var settings = TestDependencies.Settings();
        settings.DuckStation.ShowSettingsBeforeLaunch = false;

        var messageBox = TestDependencies.MessageBox();
        var handler = new DuckStationConfigHandler(
            messageBox.Object,
            TestDependencies.Logger().Object,
            Mock.Of<IServiceScopeFactory>());

        var context = new LaunchContext
        {
            FilePath = Path.Combine(_emulatorDirectory, "game.chd"),
            ResolvedFilePath = Path.Combine(_emulatorDirectory, "game.chd"),
            EmulatorName = "DuckStation",
            SystemName = "Sony PlayStation 1",
            EmulatorManager = new Emulator
            {
                EmulatorName = "DuckStation",
                EmulatorLocation = emulatorPath
            },
            Settings = settings
        };

        var shouldRun = await handler.HandleConfigurationAsync(context);

        Assert.True(shouldRun, "A configuration permission failure must not abort the game launch.");
        messageBox.Verify(m => m.FailedToInjectDuckStationConfigurationMessageBoxAsync(), Times.Once);
    }
}
