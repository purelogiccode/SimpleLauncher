using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Provides methods for launching games and files through emulators, batch files, shortcuts, and executables.
/// </summary>
public interface ILauncherService
{
    /// <summary>
    ///     Launches a ROM or game file with the specified emulator, handling extraction, parameter resolution,
    ///     emulator-specific quirks, and post-launch error analysis.
    /// </summary>
    /// <param name="resolvedFilePath">The resolved path to the game file to launch.</param>
    /// <param name="selectedEmulatorName">The display name of the emulator to use.</param>
    /// <param name="selectedSystemManager">The system manager configuration for the game.</param>
    /// <param name="selectedEmulatorManager">The emulator configuration to use for launching.</param>
    /// <param name="rawEmulatorParameters">The raw command-line parameters to pass to the emulator.</param>
    /// <param name="windowContext">The window context providing UI integration.</param>
    /// <param name="loadingStateProvider">An optional loading state provider to show launch progress.</param>
    /// <param name="originalFilePathForDisplay">An optional original file path to display instead of the resolved path.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LaunchRegularEmulatorAsync(
        string resolvedFilePath,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        ILoadingState? loadingStateProvider,
        string? originalFilePathForDisplay = null);

    /// <summary>
    ///     Validates and executes a batch file, handling working directory resolution, exit code checks, and error reporting.
    /// </summary>
    /// <param name="resolvedFilePath">The path to the batch file to run.</param>
    /// <param name="selectedEmulatorManager">The emulator configuration that determines user notification behavior.</param>
    /// <param name="windowContext">The window context providing UI integration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RunBatchFileAsync(
        string resolvedFilePath,
        Emulator selectedEmulatorManager,
        IWindowContext windowContext);

    /// <summary>
    ///     Launches a shortcut file (.LNK or .URL), resolving the target and handling protocol registration checks.
    /// </summary>
    /// <param name="resolvedFilePath">The path to the shortcut file to launch.</param>
    /// <param name="selectedEmulatorManager">The emulator configuration that determines user notification behavior.</param>
    /// <param name="windowContext">The window context providing UI integration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LaunchShortcutFileAsync(
        string resolvedFilePath,
        Emulator selectedEmulatorManager,
        IWindowContext windowContext);

    /// <summary>
    ///     Launches a standalone executable file, waits for it to exit, and reports negative exit codes as errors.
    /// </summary>
    /// <param name="resolvedFilePath">The path to the executable file to launch.</param>
    /// <param name="selectedEmulatorManager">The emulator configuration that determines user notification behavior.</param>
    /// <param name="windowContext">The window context providing UI integration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LaunchExecutableAsync(
        string resolvedFilePath,
        Emulator selectedEmulatorManager,
        IWindowContext windowContext);

    /// <summary>
    ///     Notifies the launcher that a launch strategy started the game process itself (instead of
    ///     delegating to one of the launch methods above), so play history and statistics are only
    ///     recorded for sessions where the game actually launched.
    /// </summary>
    void ReportGameLaunchStarted();
}