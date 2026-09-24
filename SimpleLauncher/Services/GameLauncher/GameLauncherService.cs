using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services;
using SimpleLauncher.Core.Services.GameLauncher;
using SimpleLauncher.Core.Services.GamePad;
using SimpleLauncher.Core.Services.SettingsManager;
using SimpleLauncher.Core.Services.UsageStats;
using SimpleLauncher.Interfaces;
using SimpleLauncher.Services.NotificationToast;
using SimpleLauncher.Services.PlayHistory;
using MameConfigurationService = SimpleLauncher.Core.Services.InjectEmulatorConfig.MameConfigurationService;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher;

/// <summary>
///     Orchestrates the game launch pipeline: validates context, resolves emulators, applies configuration,
///     selects a launch strategy, and manages post-launch cleanup and statistics.
/// </summary>
public partial class GameLauncherService : ILauncherService
{
    private const int MemoryAccessViolation = -1073741819;
    private const int DepViolation = -1073740791;

    /// <summary>
    ///     Set when the game/emulator process for the current launch pipeline was actually started.
    ///     Strategies that return early (cancelled file selection, failed mount, unsupported image)
    ///     leave it false so no play history is recorded for the dialog time.
    /// </summary>
    private bool _gameLaunchStarted;

    private readonly IEnumerable<IEmulatorConfigHandler> _configHandlers;
    private readonly IConfiguration _configuration;
    private readonly IExtractionService _extractionService;
    private readonly IEnumerable<ILaunchStrategy> _launchStrategies;
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBoxLibrary;
    private readonly IMountZipFiles _mountZipFiles;
    private readonly IParameterResolverService _parameterResolverService;
    private readonly PlayHistoryManager _playHistoryManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly Stats _stats;
    private readonly IToastNotificationService _toastNotificationService;
    private readonly IUpdateStatusBar _updateStatusBar;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GameLauncherService" /> class with all required dependencies.
    /// </summary>
    public GameLauncherService(
        IEnumerable<IEmulatorConfigHandler> configHandlers,
        IEnumerable<ILaunchStrategy> launchStrategies,
        IExtractionService extraction,
        Stats stats,
        IConfiguration configuration,
        IUpdateStatusBar updateStatusBar,
        PlayHistoryManager playHistoryManager,
        IMessageBoxLibraryService messageBoxLibrary,
        IMountZipFiles mountZipFiles,
        ILogger logger,
        IParameterResolverService parameterResolverService,
        IServiceProvider serviceProvider,
        IToastNotificationService toastNotificationService)
    {
        _configHandlers = configHandlers;
        _launchStrategies = launchStrategies.OrderBy(static s => s.Priority);
        _extractionService = extraction;
        _stats = stats;
        _configuration = configuration;
        _updateStatusBar = updateStatusBar;
        _playHistoryManager = playHistoryManager;
        _messageBoxLibrary = messageBoxLibrary;
        _mountZipFiles = mountZipFiles;
        _logger = logger;
        _parameterResolverService = parameterResolverService;
        _serviceProvider = serviceProvider;
        _toastNotificationService = toastNotificationService;
    }

    /// <summary>
    ///     Validates and executes a batch file, handling working directory resolution, exit code checks, and error reporting.
    /// </summary>
    public async Task RunBatchFileAsync(string resolvedFilePath, Emulator selectedEmulatorManager,
        IWindowContext windowContext)
    {
        var invalidPaths = ValidateBatchFile.FindInvalidQuotedPathsSimple(resolvedFilePath);
        if (invalidPaths.Count > 0)
        {
            var shouldContinue = await _messageBoxLibrary.BatchFilePathsMissingMessageBoxAsync(invalidPaths);
            if (!shouldContinue) return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = resolvedFilePath,
            UseShellExecute = true
        };

        try
        {
            var workingDirectory = Path.GetDirectoryName(resolvedFilePath);
            if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Could not get workingDirectory for batch file: '{resolvedFilePath}'. Using default.");
            psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        if (!string.IsNullOrEmpty(psi.WorkingDirectory) && !Directory.Exists(psi.WorkingDirectory))
        {
            var logPath = PathHelper.ResolveLogFilePath(_configuration);
            var msg = $"The working directory for the batch file does not exist: {psi.WorkingDirectory}";
            var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                ? "User was notified."
                : "User was not notified.";
            // Expected user condition (batch file folder moved/deleted/not configured) —
            // Information level so it is never reported as a bug.
            _logger.Information(new DirectoryNotFoundException(msg), $"{msg}\n{userNotified}");
            if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
            {
                await _messageBoxLibrary.BatchFileFailedMessageBoxAsync(resolvedFilePath,
                    $"Working directory not found: {psi.WorkingDirectory}", logPath);
            }

            return;
        }

        _logger.Debug("RunBatchFileAsync:\n\n");
        _logger.Debug($"Command: {psi.FileName}");
        _logger.Debug($"Working Directory: {psi.WorkingDirectory}\n");

        try
        {
            using var process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException("Failed to start the batch file process.");
            _gameLaunchStarted = true;

            // 5-minute timeout to prevent hung batch files from blocking indefinitely
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected user condition (a hung or long-running batch file) —
                // Information level so it is never reported as a bug.
                _logger.Information(new TimeoutException($"Batch file timed out after 5 minutes: {resolvedFilePath}"),
                    $"Batch file timed out: {resolvedFilePath}");
                try
                {
                    process.Kill(true);
                }
                catch (Exception killEx)
                {
                    _logger?.Debug($"Failed to kill timed-out batch process: {killEx.Message}");
                }

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    var logPath =
                        PathHelper.ResolveLogFilePath(_configuration);
                    await _messageBoxLibrary.BatchFileFailedMessageBoxAsync(resolvedFilePath,
                        "Process timed out after 5 minutes", logPath);
                }

                return;
            }

            var batchShortName = Path.GetFileName(resolvedFilePath);
            if (process.ExitCode != 0)
            {
                var logPath =
                    PathHelper.ResolveLogFilePath(_configuration);
                var errorDetail = $"Batch file exited with code {process.ExitCode}.\nBatch file: {resolvedFilePath}";
                var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                    ? "User was notified."
                    : "User was not notified.";
                _logger.Error(new InvalidOperationException($"Batch file exited with code {process.ExitCode}"),
                    $"{errorDetail}\n{userNotified}");

                LogBatchFileContentsOnError(resolvedFilePath);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    await _messageBoxLibrary.BatchFileFailedMessageBoxAsync(resolvedFilePath,
                        $"Exit code: {process.ExitCode}", logPath, process.ExitCode);
                }

                _toastNotificationService.ShowToast("Simple Launcher", $"Error: {batchShortName} failed");
                _updateStatusBar.UpdateContent($"Error: {batchShortName} failed");
            }
            else
            {
                var launched = (string)Application.Current.TryFindResource("Launched") ?? "launched";
                _toastNotificationService.ShowToast("Simple Launcher", $"{batchShortName} {launched}");
                _updateStatusBar.UpdateContent($"{batchShortName} {launched}");
            }
        }
        catch (Win32Exception ex)
        {
            if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex))
            {
                await _messageBoxLibrary.ApplicationControlPolicyBlockedMessageBoxAsync();
                // Expected user-environment condition (OS policy blocks the file): not a bug.
                _logger.Information(ex, "Application control policy blocked launching batch file");
                _updateStatusBar.UpdateContent($"Error: {Path.GetFileName(resolvedFilePath)} failed");
            }
            else if (CheckApplicationControlPolicyService.IsElevationRequired(ex))
            {
                await _messageBoxLibrary.ElevationRequiredMessageBoxAsync();
                // Expected user-environment condition (the file requires admin rights): not a bug.
                _logger.Information(ex, "Elevation required to launch batch file");
                _updateStatusBar.UpdateContent($"Error: {Path.GetFileName(resolvedFilePath)} failed");
            }
            else if (CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex))
            {
                // User canceled the operation (e.g., clicked Cancel on UAC prompt) - do nothing, don't log
            }
            else if (CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex))
            {
                // Expected user-error condition (the batch file is not a valid application for this
                // OS platform): not a bug, keep it out of the bug report service.
                _logger.Information(ex, "Invalid executable format: {BatchFile}", resolvedFilePath);
                await _messageBoxLibrary.InvalidExecutableFileMessageBoxAsync();
                _updateStatusBar.UpdateContent($"Error: {Path.GetFileName(resolvedFilePath)} failed");
            }
            else
            {
                var errorDetail =
                    $"Exception running the batch process.\nBatch file: {resolvedFilePath}\nException: {ex.Message}";
                var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                    ? "User was notified."
                    : "User was not notified.";
                _logger.Error(ex, $"{errorDetail}\n{userNotified}");

                LogBatchFileContentsOnError(resolvedFilePath);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    var logPath =
                        PathHelper.ResolveLogFilePath(_configuration);
                    await _messageBoxLibrary.BatchFileFailedMessageBoxAsync(resolvedFilePath, ex.Message, logPath);
                }

                _updateStatusBar.UpdateContent($"Error: {Path.GetFileName(resolvedFilePath)} failed");
            }
        }
        catch (Exception ex)
        {
            var errorDetail =
                $"Exception running the batch process.\nBatch file: {resolvedFilePath}\nException: {ex.Message}";
            var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                ? "User was notified."
                : "User was not notified.";
            _logger.Error(ex, $"{errorDetail}\n{userNotified}");

            LogBatchFileContentsOnError(resolvedFilePath);

            if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
            {
                var logPath =
                    PathHelper.ResolveLogFilePath(_configuration);
                await _messageBoxLibrary.BatchFileFailedMessageBoxAsync(resolvedFilePath, ex.Message, logPath);
            }

            _updateStatusBar.UpdateContent($"Error: {Path.GetFileName(resolvedFilePath)} failed");
        }
    }

    /// <summary>
    ///     Launches a shortcut file (.LNK or .URL), resolving the target and handling protocol registration checks.
    /// </summary>
    public async Task LaunchShortcutFileAsync(string resolvedFilePath, Emulator selectedEmulatorManager,
        IWindowContext windowContext)
    {
        // Common UI updates.
        var launched = (string)Application.Current.TryFindResource("Launched") ?? "launched";
        var fileName = Path.GetFileName(resolvedFilePath);
        _toastNotificationService.ShowToast("Simple Launcher", $"{fileName} {launched}");
        _updateStatusBar.UpdateContent($"{fileName} {launched}");

        try
        {
            var extension = Path.GetExtension(resolvedFilePath).ToUpperInvariant();

            // Validate file exists first
            if (!File.Exists(PathHelper.GetLongPath(resolvedFilePath)))
                throw new FileNotFoundException($"Shortcut file not found: {resolvedFilePath}");

            if (string.Equals(extension, ".URL", StringComparison.Ordinal))
            {
                // Read and validate the .url file content
                var urlContent = await File.ReadAllTextAsync(resolvedFilePath);
                var urlMatch = MyRegex().Match(urlContent);

                if (!urlMatch.Success || string.IsNullOrWhiteSpace(urlMatch.Groups[1].Value))
                {
                    throw new InvalidOperationException(
                        $"Invalid .url file format or missing URL in: {resolvedFilePath}");
                }

                var targetUrl = urlMatch.Groups[1].Value.Trim();
                _logger.Debug(
                    $"LaunchShortcutFileAsync (.URL):\n\nShortcut File: {resolvedFilePath}\nTarget URL: {targetUrl}\n");

                // Verify protocol handler is registered ONLY if it's a real URI (contains ://)
                // This prevents treating drive letters (C:\) as protocols.
                var protocolIndex = targetUrl.IndexOf("://", StringComparison.Ordinal);
                if (protocolIndex > 0)
                {
                    var protocol = targetUrl[..protocolIndex];
                    if (!IsProtocolRegistered(protocol))
                    {
                        await _messageBoxLibrary.ProtocolHandlerNotRegisteredMessageBoxAsync(protocol);
                        return;
                    }
                }

                // Use shell execution with explicit working directory set to null
                var psi = new ProcessStartInfo
                {
                    FileName = targetUrl, // Launch the URL directly, not the .url file
                    UseShellExecute = true,
                    WorkingDirectory = null // Explicitly no working directory
                };

                using var process = new Process();
                process.StartInfo = psi;

                // For shell execution, Start() might return false if a process was reused.
                // Win32Exception will be thrown if it actually fails.
                process.Start();
                _gameLaunchStarted = true;
            }
            else // .LNK files
            {
                var psi = new ProcessStartInfo
                {
                    FileName = resolvedFilePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(resolvedFilePath) ?? AppDomain.CurrentDomain.BaseDirectory
                };

                _logger.Debug(
                    $"LaunchShortcutFileAsync (.LNK):\n\nShortcut File: {psi.FileName}\nWorking Directory: {psi.WorkingDirectory}\n");

                using var process = new Process();
                process.StartInfo = psi;

                // For shell execution, Start() might return false if a process was reused.
                process.Start();
                _gameLaunchStarted = true;
            }
        }
        catch (Win32Exception ex) // Catch Win32Exception specifically
        {
            if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex))
            {
                await _messageBoxLibrary.ApplicationControlPolicyBlockedMessageBoxAsync();
                // Expected user-environment condition (OS policy blocks the file): not a bug.
                _logger.Information(ex, "Application control policy blocked launching shortcut file");
            }
            else if (CheckApplicationControlPolicyService.IsElevationRequired(ex))
            {
                await _messageBoxLibrary.ElevationRequiredMessageBoxAsync();
                // Expected user-environment condition (the file requires admin rights): not a bug.
                _logger.Information(ex, "Elevation required to launch shortcut file");
            }
            else if (CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex))
            {
                // User canceled the operation (e.g., clicked Cancel on UAC prompt) - do nothing, don't log
            }
            else if (CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex))
            {
                // Expected user-error condition (the shortcut target is not a valid application for
                // this OS platform): not a bug, keep it out of the bug report service.
                _logger.Information(ex, "Invalid executable format: {ShortcutFile}", resolvedFilePath);
                await _messageBoxLibrary.InvalidExecutableFileMessageBoxAsync();
            }
            else
            {
                // Existing error handling for other Win32Exceptions
                var longPathForContent = PathHelper.GetLongPath(resolvedFilePath);
                var fileContent = longPathForContent != null && File.Exists(longPathForContent)
                    ? $"\nFile Content:\n{await File.ReadAllTextAsync(longPathForContent)}"
                    : "\nFile does not exist.";
                var errorDetail = "Exception launching the shortcut file.\n" +
                                  $"Shortcut file: {resolvedFilePath}\n" +
                                  $"Exception: {ex.Message}" +
                                  fileContent;
                var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                    ? "User was notified."
                    : "User was not notified.";
                var contextMessage = $"{errorDetail}\n{userNotified}";
                _logger.Error(ex, contextMessage);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    var launchErrorTitle = (string)Application.Current.TryFindResource("LaunchErrorTitle") ??
                                           "Launch Error";
                    await _messageBoxLibrary.ShowCustomMessageBoxAsync("There was a Win32Exception.", launchErrorTitle,
                        PathHelper.ResolveLogFilePath(_configuration));
                }
            }
        }
        catch (Exception ex)
        {
            // Only attempt to read file content if it's a text-based .URL file
            var isUrlFile = Path.GetExtension(resolvedFilePath).Equals(".url", StringComparison.OrdinalIgnoreCase);
            var fileContent = isUrlFile && File.Exists(resolvedFilePath)
                ? $"\nFile Content:\n{await File.ReadAllTextAsync(resolvedFilePath)}"
                : "\nFile content not displayed (binary .LNK or missing file).";

            var errorDetail = "Exception launching the shortcut file.\n" +
                              $"Shortcut file: {resolvedFilePath}\n" +
                              $"Exception: {ex.Message}" +
                              fileContent;
            var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                ? "User was notified."
                : "User was not notified.";
            var contextMessage = $"{errorDetail}\n{userNotified}";
            _logger.Error(ex, contextMessage);

            if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
            {
                var launchErrorTitle =
                    (string)Application.Current.TryFindResource("LaunchErrorTitle") ?? "Launch Error";
                var couldNotLaunchShortcut = (string)Application.Current.TryFindResource("CouldNotLaunchShortcut") ??
                                             "Could not launch the game shortcut. The protocol handler may not be installed. Please ensure the game launcher (Steam, GOG Galaxy, etc.) is installed.";
                var userMessage = ex switch
                {
                    FileNotFoundException => $"Shortcut file not found: {Path.GetFileName(resolvedFilePath)}.",
                    _ => couldNotLaunchShortcut
                };

                await _messageBoxLibrary.ShowCustomMessageBoxAsync(userMessage, launchErrorTitle,
                    PathHelper.ResolveLogFilePath(_configuration));
            }
        }
    }

    /// <summary>
    ///     Launches a standalone executable file, waits for it to exit, and reports negative exit codes as errors.
    /// </summary>
    public async Task LaunchExecutableAsync(string resolvedFilePath, Emulator selectedEmulatorManager,
        IWindowContext windowContext)
    {
        var psi = new ProcessStartInfo
        {
            FileName = resolvedFilePath,
            Arguments = "",
            UseShellExecute = false, // Keep false to be able to read ExitCode
            CreateNoWindow = false // Let the app show its own window
        };

        try
        {
            var workingDirectory = Path.GetDirectoryName(resolvedFilePath);
            if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
        }
        catch (Exception ex)
        {
            _logger.Error(ex,
                $"Could not get workingDirectory for executable file: '{resolvedFilePath}'. Using default.");
            psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        var launched = (string)Application.Current.TryFindResource("Launched") ?? "launched";
        _logger.Debug("LaunchExecutableAsync:\n\n");
        _logger.Debug($"Executable File: {psi.FileName}");
        _logger.Debug($"Working Directory: {psi.WorkingDirectory}\n");

        _toastNotificationService.ShowToast("Simple Launcher", $"{Path.GetFileName(psi.FileName)} {launched}");
        _updateStatusBar.UpdateContent($"{Path.GetFileName(psi.FileName)} {launched}");

        using var process = new Process();
        process.StartInfo = psi;

        try
        {
            var processStarted = process.Start();
            if (!processStarted) throw new InvalidOperationException("Failed to start the executable process.");
            _gameLaunchStarted = true;

            await process.WaitForExitAsync();

            // Only check for critical failures (crashes, not normal exit codes like 1)
            if (process.ExitCode < 0) // Negative exit codes typically indicate system-level failures
            {
                var errorDetail = "Executable process exited with system error code.\n" +
                                  $"Executable file: {psi.FileName}\n" +
                                  $"Exit code: {process.ExitCode}";
                var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                    ? "User was notified."
                    : "User was not notified.";
                var contextMessage = $"{errorDetail}\n{userNotified}";
                _logger.Warning(contextMessage);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    await _messageBoxLibrary.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                        PathHelper.ResolveLogFilePath(_configuration));
                }
            }
        }
        catch (Win32Exception ex)
        {
            if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex))
            {
                await _messageBoxLibrary.ApplicationControlPolicyBlockedMessageBoxAsync();
                // Expected user-environment condition (OS policy blocks the file): not a bug.
                _logger.Information(ex, "Application control policy blocked launching executable");
            }
            else if (CheckApplicationControlPolicyService.IsElevationRequired(ex))
            {
                await _messageBoxLibrary.ElevationRequiredMessageBoxAsync();
                // Expected user-environment condition (the file requires admin rights): not a bug.
                _logger.Information(ex, "Elevation required to launch executable");
            }
            else if (CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex))
            {
                // User canceled the operation (e.g., clicked Cancel on UAC prompt) - do nothing, don't log
            }
            else if (CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex))
            {
                // Expected user-error condition (the file is not a valid executable for this OS platform,
                // e.g. a non-Win32 or wrong-architecture binary): not a bug, keep it out of the bug report service.
                string exitCodeInfo;
                try
                {
                    exitCodeInfo =
                        $"Exit code: {(process.HasExited ? process.ExitCode.ToString(CultureInfo.InvariantCulture) : "N/A")}";
                }
                catch (InvalidOperationException)
                {
                    exitCodeInfo = "Exit code: N/A (Process not associated)";
                }

                var errorDetail = "Exception launching the executable file.\n" +
                                  $"Executable file: {psi.FileName}\n" +
                                  $"{exitCodeInfo}\n" +
                                  $"Exception: {ex.Message}";
                var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                    ? "User was notified."
                    : "User was not notified.";
                var contextMessage = $"{errorDetail}\n\n{userNotified}";
                _logger.Information(contextMessage);

                // Notify user with a direct, actionable message (the log file will not contain this at Information level)
                await _messageBoxLibrary.InvalidExecutableFileMessageBoxAsync();
            }
            else
            {
                string exitCodeInfo;
                try
                {
                    exitCodeInfo =
                        $"Exit code: {(process.HasExited ? process.ExitCode.ToString(CultureInfo.InvariantCulture) : "N/A")}";
                }
                catch (InvalidOperationException)
                {
                    exitCodeInfo = "Exit code: N/A (Process not associated)";
                }

                var errorDetail = "Exception launching the executable file.\n" +
                                  $"Executable file: {psi.FileName}\n" +
                                  $"{exitCodeInfo}\n" +
                                  $"Exception: {ex.Message}";
                var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                    ? "User was notified."
                    : "User was not notified.";
                var contextMessage = $"{errorDetail}\n\n{userNotified}";
                _logger.Error(ex, contextMessage);

                if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                {
                    await _messageBoxLibrary.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                        PathHelper.ResolveLogFilePath(_configuration));
                }
            }
        }
        catch (Exception ex)
        {
            string exitCodeInfo;
            try
            {
                exitCodeInfo =
                    $"Exit code: {(process.HasExited ? process.ExitCode.ToString(CultureInfo.InvariantCulture) : "N/A")}";
            }
            catch (InvalidOperationException)
            {
                exitCodeInfo = "Exit code: N/A (Process not associated)";
            }

            var errorDetail = "Exception launching the executable file.\n" +
                              $"Executable file: {psi.FileName}\n" +
                              $"{exitCodeInfo}\n" +
                              $"Exception: {ex.Message}";
            var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                ? "User was notified."
                : "User was not notified.";
            var contextMessage = $"{errorDetail}\n\n{userNotified}";
            _logger.Error(ex, contextMessage);

            if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
            {
                await _messageBoxLibrary.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
            }
        }
    }

    /// <inheritdoc />
    public void ReportGameLaunchStarted()
    {
        _gameLaunchStarted = true;
    }

    /// <summary>
    ///     Launches a ROM or game file with the specified emulator, handling extraction, parameter resolution,
    ///     emulator-specific quirks, and post-launch error analysis.
    /// </summary>
    public async Task LaunchRegularEmulatorAsync(
        string resolvedFilePath,
        string selectedEmulatorName,
        ISystemManager selectedSystemManager,
        Emulator selectedEmulatorManager,
        string rawEmulatorParameters,
        IWindowContext windowContext,
        ILoadingState? loadingStateProvider,
        string? originalFilePathForDisplay = null)
    {
        // Use the original file path for display if provided (e.g., for mounted files),
        // otherwise use the resolved file path.
        // This ensures we show the original archive name, not the temp extracted or mounted file.
        var displayFilePath = originalFilePathForDisplay ?? resolvedFilePath;
        var originalFileName = Path.GetFileNameWithoutExtension(displayFilePath);

        var isDirectory = Directory.Exists(resolvedFilePath);

        if (string.IsNullOrEmpty(selectedEmulatorName))
        {
            // Notify developer
            const string contextMessage = "[LaunchRegularEmulatorAsync] selectedEmulatorName is null or empty.";
            _logger.Warning(contextMessage);
            _logger.Debug($"[LaunchRegularEmulatorAsync] Error: {contextMessage}");

            // Notify user
            await _messageBoxLibrary.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            return;
        }

        // A simple and effective way to identify a mounted XBE path from our tool
        // is by its characteristic filename. This avoids hardcoding drive letters.
        var isMountedXbe = Path.GetFileName(resolvedFilePath).Equals("default.xbe", StringComparison.OrdinalIgnoreCase);

        // Check if the file to launch is a mounted ZIP file, which will not be extracted
        var isMountedZip =
            resolvedFilePath.StartsWith(_mountZipFiles.ConfiguredMountDriveRoot, StringComparison.OrdinalIgnoreCase);

        // Check if it's a file we just converted to temp
        var isTempConvertedFile = resolvedFilePath.Contains(Path.Combine(Path.GetTempPath(), "SimpleLauncher"),
            StringComparison.OrdinalIgnoreCase);

        var isChd = Path.GetExtension(resolvedFilePath).Equals(".chd", StringComparison.OrdinalIgnoreCase);
        var isCue = Path.GetExtension(resolvedFilePath).Equals(".cue", StringComparison.OrdinalIgnoreCase);
        var isBin = Path.GetExtension(resolvedFilePath).Equals(".bin", StringComparison.OrdinalIgnoreCase);
        var isIso = Path.GetExtension(resolvedFilePath).Equals(".iso", StringComparison.OrdinalIgnoreCase);
        var isZip = Path.GetExtension(resolvedFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
        var is7Z = Path.GetExtension(resolvedFilePath).Equals(".7z", StringComparison.OrdinalIgnoreCase);
        var isRar = Path.GetExtension(resolvedFilePath).Equals(".rar", StringComparison.OrdinalIgnoreCase);

        var isAzahar = selectedEmulatorManager?.EmulatorLocation != null &&
                       (selectedEmulatorName.Contains("Azahar", StringComparison.OrdinalIgnoreCase) ||
                        selectedEmulatorManager.EmulatorLocation.Contains("azahar.exe",
                            StringComparison.OrdinalIgnoreCase));

        var isCitra = selectedEmulatorManager?.EmulatorLocation != null &&
                      (selectedEmulatorName.Contains("Citra", StringComparison.OrdinalIgnoreCase) ||
                       selectedEmulatorManager.EmulatorLocation.Contains("citra.exe",
                           StringComparison.OrdinalIgnoreCase) ||
                       selectedEmulatorManager.EmulatorLocation.Contains("citra-qt.exe",
                           StringComparison.OrdinalIgnoreCase));

        var isDuckstation = selectedEmulatorManager?.EmulatorLocation != null &&
                            (selectedEmulatorName.Contains("duckstation", StringComparison.OrdinalIgnoreCase) ||
                             selectedEmulatorManager.EmulatorLocation.Contains("duckstation",
                                 StringComparison.OrdinalIgnoreCase));

        var isGeolith = selectedEmulatorManager?.EmulatorLocation != null &&
                        (selectedEmulatorManager.EmulatorParameters.Contains("geolith_libretro",
                             StringComparison.OrdinalIgnoreCase) ||
                         selectedEmulatorManager.EmulatorParameters.Contains("geolith_libretro.dll",
                             StringComparison.OrdinalIgnoreCase));

        var isMame = selectedEmulatorManager?.EmulatorLocation != null &&
                     (selectedEmulatorName.Equals("MAME", StringComparison.OrdinalIgnoreCase) ||
                      selectedEmulatorName.Equals("M.A.M.E.", StringComparison.OrdinalIgnoreCase) ||
                      selectedEmulatorManager.EmulatorLocation.Contains("mame.exe",
                          StringComparison.OrdinalIgnoreCase) ||
                      selectedEmulatorManager.EmulatorLocation.Contains("mame64.exe",
                          StringComparison.OrdinalIgnoreCase));

        var isOotake = selectedEmulatorManager?.EmulatorLocation != null &&
                       (selectedEmulatorName.Contains("Ootake", StringComparison.OrdinalIgnoreCase) ||
                        selectedEmulatorManager.EmulatorLocation.Contains("ootake.exe",
                            StringComparison.OrdinalIgnoreCase));

        var isRaine = selectedEmulatorManager?.EmulatorLocation != null &&
                      (selectedEmulatorName.Contains("Raine", StringComparison.OrdinalIgnoreCase) ||
                       selectedEmulatorManager.EmulatorLocation.Contains("raine", StringComparison.OrdinalIgnoreCase));

        var isRetroArch = selectedEmulatorManager?.EmulatorLocation != null &&
                          (selectedEmulatorManager.EmulatorName.Contains("retroarch",
                               StringComparison.OrdinalIgnoreCase) ||
                           selectedEmulatorManager.EmulatorLocation.Contains("retroarch",
                               StringComparison.OrdinalIgnoreCase));

        var isSameboy = selectedEmulatorManager?.EmulatorLocation != null &&
                        (selectedEmulatorName.Contains("Sameboy", StringComparison.OrdinalIgnoreCase) ||
                         selectedEmulatorManager.EmulatorLocation.Contains("sameboy.exe",
                             StringComparison.OrdinalIgnoreCase));

        var isXemu = selectedEmulatorManager?.EmulatorLocation != null &&
                     (selectedEmulatorName.Contains("Xemu", StringComparison.OrdinalIgnoreCase) ||
                      selectedEmulatorManager.EmulatorLocation.Contains("xemu", StringComparison.OrdinalIgnoreCase));

        var isYmir = selectedEmulatorManager?.EmulatorLocation != null &&
                     (selectedEmulatorName.Contains("Ymir", StringComparison.OrdinalIgnoreCase) ||
                      selectedEmulatorName.Contains("Yumir", StringComparison.OrdinalIgnoreCase) ||
                      IsYmirEmulatorLocation(selectedEmulatorManager.EmulatorLocation));

        // Declare tempExtractionPath here to be accessible in the finally block
        string? tempExtractionPath = null;

        var fileExtension = Path.GetExtension(resolvedFilePath).ToLowerInvariant();

        if (isRetroArch &&
            selectedEmulatorManager?.EmulatorParameters.Contains("-L", StringComparison.OrdinalIgnoreCase) == false)
        {
            var errorMessage =
                $"[LaunchRegularEmulatorAsync] RetroArch parameter should contain -L. Parameter field: {selectedEmulatorManager.EmulatorParameters}";
            _logger.Debug(errorMessage);
            _logger.Information(errorMessage);

            await _messageBoxLibrary.RetroArchParameterShouldContainLMessageBoxAsync();

            // Offer AI parameter fix
            await AskAiToFixParameters.ExecuteAsync(selectedSystemManager, selectedEmulatorManager, _messageBoxLibrary,
                _parameterResolverService, _configuration, _serviceProvider, _logger);

            return;
        }

        if (isXemu && selectedEmulatorManager?.EmulatorParameters.Contains("-dvd_path",
                StringComparison.OrdinalIgnoreCase) == false)
        {
            var errorMessage =
                $"[LaunchRegularEmulatorAsync] Xemu parameter should contain '-dvd_path'. Parameter field: {selectedEmulatorManager.EmulatorParameters}";
            _logger.Debug(errorMessage);
            _logger.Information(errorMessage);

            await _messageBoxLibrary.XemuParameterShouldContainDvdPathMessageBoxAsync();

            // Offer AI parameter fix
            await AskAiToFixParameters.ExecuteAsync(selectedSystemManager, selectedEmulatorManager, _messageBoxLibrary,
                _parameterResolverService, _configuration, _serviceProvider, _logger);

            return;
        }

        if ((selectedSystemManager.ExtractFileBeforeLaunch || isAzahar || isCitra || isDuckstation || isOotake ||
             isSameboy || isYmir) && !isDirectory && !isMountedXbe && !isMountedZip && !isTempConvertedFile)
        {
            if (fileExtension is ".zip" or ".rar" or ".7z")
            {
                var extractingMsg = (string)Application.Current.TryFindResource("ExtractingEllipsis") ??
                                    "Extracting file... Please wait.";
                loadingStateProvider?.SetLoadingState(true, extractingMsg);
                _updateStatusBar.UpdateContent(extractingMsg);

                try
                {
                    var (extractedGameFilePath, extractedTempDirPath) =
                        await _extractionService.ExtractToTempAndGetLaunchFileAsync(resolvedFilePath,
                            selectedSystemManager.FileFormatsToLaunch);

                    if (!string.IsNullOrEmpty(extractedGameFilePath)) resolvedFilePath = extractedGameFilePath;

                    // Always store the temp directory path for cleanup, even if no game file was found within it
                    tempExtractionPath = extractedTempDirPath;
                }
                finally
                {
                    // End extraction loading state before starting launch state
                    loadingStateProvider?.SetLoadingState(false);
                }

                // Update message for launching without incrementing count (caller already has loading state active)
                var launchingMsg = (string)Application.Current.TryFindResource("Launching") ?? "Launching...";
                _updateStatusBar.UpdateContent(launchingMsg);
            }
        }

        if (isOotake && (isChd || isBin || isCue || isIso))
        {
            await _messageBoxLibrary.OotakeDoesNotSupportImageFilesMessageBoxAsync();
            return;
        }

        if (isGeolith && (isZip || is7Z || isRar))
        {
            await _messageBoxLibrary.GeolithDoesNotSupportCompressedFilesMessageBoxAsync();
            return;
        }

        if (string.IsNullOrEmpty(resolvedFilePath))
        {
            // Notify developer
            const string contextMessage =
                "resolvedFilePath is null or empty after extraction attempt (or for mounted files).";
            _logger.Warning(contextMessage);
            _logger.Debug($"[LaunchRegularEmulatorAsync] Error: {contextMessage}");

            // Notify user
            await _messageBoxLibrary.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            // The finally block will handle cleanup of tempExtractionPath if it was set.
            return;
        }

        // For mounted files, ensure it still exists before proceeding
        if ((isMountedXbe || isMountedZip) && !File.Exists(PathHelper.GetLongPath(resolvedFilePath)))
        {
            // Notify developer
            var contextMessage = $"Mounted file {resolvedFilePath} not found when trying to launch with emulator.";
            _logger.Warning(contextMessage);
            _logger.Debug($"[LaunchRegularEmulatorAsync] Error: {contextMessage}");

            // Notify user
            await _messageBoxLibrary.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            return;
        }

        // Resolve the Emulator Path (executable)
        if (selectedEmulatorManager != null)
        {
            // Check if emulator location is empty or null
            if (string.IsNullOrWhiteSpace(selectedEmulatorManager.EmulatorLocation))
            {
                // Notify developer
                var contextMessage = $"EmulatorLocation is null or empty for emulator '{selectedEmulatorName}'. " +
                                     "This typically means the system was configured to run directly executable files (.bat, .exe, .lnk) " +
                                     "but the user is trying to launch a non-executable file that requires an emulator.";
                _logger.Warning(contextMessage);
                _logger.Debug($"[LaunchRegularEmulatorAsync] Error: {contextMessage}");

                // Notify user with a helpful message
                await _messageBoxLibrary.EmulatorPathNotConfiguredMessageBoxAsync();

                return;
            }

            var resolvedEmulatorExePath =
                PathHelper.ResolveRelativeToAppDirectory(selectedEmulatorManager.EmulatorLocation);

            // If the file doesn't exist, try Unicode normalization variations.
            // This handles cases where filenames have different normalization forms (NFC vs NFD),
            // commonly occurring when files are created on different operating systems (macOS vs Windows)
            // or synced through services such as OneDrive.
            if (!string.IsNullOrEmpty(resolvedEmulatorExePath) &&
                !File.Exists(PathHelper.GetLongPath(resolvedEmulatorExePath)))
            {
                var normalizedEmulatorPath = PathHelper.TryFindFileWithNormalizedPath(resolvedEmulatorExePath);
                if (!string.IsNullOrEmpty(normalizedEmulatorPath))
                {
                    resolvedEmulatorExePath = normalizedEmulatorPath;
                    _logger.Debug(
                        $"[LaunchRegularEmulatorAsync] Found emulator using Unicode normalization: {normalizedEmulatorPath}");
                }
            }

            if (string.IsNullOrEmpty(resolvedEmulatorExePath) ||
                !File.Exists(PathHelper.GetLongPath(resolvedEmulatorExePath)))
            {
                // Notify developer
                var contextMessage =
                    $"Emulator executable path is null, empty, or does not exist after resolving: '{selectedEmulatorManager.EmulatorLocation}' -> '{resolvedEmulatorExePath}'";

                // Check if the path is in a OneDrive folder and provide specific guidance
                if (!string.IsNullOrEmpty(resolvedEmulatorExePath) &&
                    resolvedEmulatorExePath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDir = Path.GetDirectoryName(resolvedEmulatorExePath);
                    var oneDriveFolderExists = !string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir);
                    contextMessage += oneDriveFolderExists
                        ? "\nThe emulator file is in a OneDrive folder but could not be found. Ensure the file is synced and downloaded to your device. Right-click the file in File Explorer and select 'Always keep on this device'."
                        : "\nThe parent OneDrive folder does not exist or is not accessible. Ensure OneDrive is signed in and synced, and that the folder is available on this device.";
                }

                _logger.Debug($"[LaunchRegularEmulatorAsync] Error: {contextMessage}");

                // Expected user condition (emulator moved/renamed/deleted/unconfigured) — Information level.
                _logger.Information(contextMessage);

                // Notify user with a direct, actionable message (the log file will not contain this at Information level)
                await _messageBoxLibrary.EmulatorExecutableNotFoundMessageBoxAsync(
                    selectedEmulatorManager.EmulatorLocation);

                return;
            }

            // Determine the emulator's directory, which is the base for %EMULATORFOLDER%
            var resolvedEmulatorFolderPath = Path.GetDirectoryName(resolvedEmulatorExePath);
            if (string.IsNullOrEmpty(resolvedEmulatorFolderPath) ||
                !Directory.Exists(PathHelper.GetLongPath(resolvedEmulatorFolderPath))) // Should exist if exe exists
            {
                // Notify developer
                var contextMessage =
                    $"Could not determine emulator folder path from executable path: '{resolvedEmulatorExePath}'";
                _logger.Warning(contextMessage);
                _logger.Debug($"[LaunchRegularEmulatorAsync] Error: {contextMessage}");

                // Notify user
                await _messageBoxLibrary.CouldNotLaunchThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));

                return;
            }

            // Resolve the system folder that contains this specific ROM
            var romSystemFolder = PathHelper.FindContainingSystemFolder(selectedSystemManager.SystemFolders,
                selectedSystemManager.PrimarySystemFolder, resolvedFilePath);

            // Compute the ROM name (filename without extension, or folder name for directories)
            // before ResolveParameterString so %NAME% can be resolved
            var romName = isDirectory
                ? Path.GetFileName(resolvedFilePath)
                : Path.GetFileNameWithoutExtension(resolvedFilePath);

            // Resolve Emulator Parameters using the PathHelper.ResolveParameterString
            var resolvedParameters = PathHelper.ResolveParameterString(
                rawEmulatorParameters, // The raw parameter string from config
                selectedSystemManager.SystemFolders, // All configured system folders
                resolvedEmulatorFolderPath, // The fully resolved emulator directory path
                resolvedFilePath, // The ROM path for %ROM%
                romSystemFolder, // The system folder containing this ROM for %ROMSYSTEMFOLDER%
                romName // The ROM name (filename without extension) for %NAME%
            );

            string arguments;

            // Detect NeoGeo CD based on extension
            var ext = Path.GetExtension(resolvedFilePath).ToLowerInvariant();
            var isNeoGeoCd = ext is ".cue" or ".iso" or ".bin";

            // If the original parameters contained %ROM%, it was already resolved by ResolveParameterString
            // and we should not append the file path again.
            var containsRomPlaceholder = rawEmulatorParameters.Contains("%ROM%", StringComparison.OrdinalIgnoreCase);
            var containsNamePlaceholder = rawEmulatorParameters.Contains("%NAME%", StringComparison.OrdinalIgnoreCase);

            if (containsRomPlaceholder || containsNamePlaceholder ||
                PathHelper.ContainsGameSpecificPlaceholder(resolvedParameters))
            {
                arguments = resolvedParameters;
            }
            else
            {
                // Trim trailing spaces and check if it ends with '=' to avoid adding an extra space
                var trimmedParameters = resolvedParameters.TrimEnd();
                var space = string.IsNullOrWhiteSpace(trimmedParameters) || trimmedParameters.EndsWith('=')
                    ? ""
                    : " ";

                // Will load the filename without the extension
                if ((isMame || isRaine) && !isNeoGeoCd)
                {
                    _logger.Debug($"Stripped path call detected. Launching: {romName}");
                    arguments = $"{trimmedParameters}{space}\"{romName}\"";
                }
                else
                {
                    // General call or Raine NeoGeo CD - Provide full filepath
                    arguments = $"{trimmedParameters}{space}\"{resolvedFilePath}\"";
                }
            }

            string workingDirectory;
            try
            {
                // Set the working directory to the directory of the emulator executable
                workingDirectory = resolvedEmulatorFolderPath;
            }
            catch (Exception ex)
            {
                // Notify developer
                _logger.Error(ex,
                    $"Could not get workingDirectory for emulator: '{resolvedEmulatorFolderPath}'. Using default.");
                _logger.Debug(
                    $"Could not get workingDirectory for emulator: '{resolvedEmulatorFolderPath}'. Using default.");

                workingDirectory = AppDomain.CurrentDomain.BaseDirectory; // fallback
            }

            var psi = new ProcessStartInfo
            {
                FileName = resolvedEmulatorExePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _logger.Debug("LaunchRegularEmulatorAsync:\n\n" +
                          $"Program Location: {resolvedEmulatorExePath}\n" +
                          $"Arguments: {arguments}\n" +
                          $"Working Directory: {psi.WorkingDirectory}\n" +
                          $"File to launch: {resolvedFilePath}");

            var launchedwith = (string)Application.Current.TryFindResource("launchedwith") ?? "launched with";

            _toastNotificationService.ShowToast("Simple Launcher",
                $"{originalFileName} {launchedwith} {selectedEmulatorName}");
            _updateStatusBar.UpdateContent($"{originalFileName} {launchedwith} {selectedEmulatorName}");

            StringBuilder output = new();
            StringBuilder error = new();

            using (var process = new Process())
            {
                process.StartInfo = psi;

                process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data)) output.AppendLine(args.Data);
                };

                process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data)) error.AppendLine(args.Data);
                };

                try
                {
                    var processStarted = process.Start();
                    if (!processStarted) throw new InvalidOperationException("Failed to start the process.");
                    _gameLaunchStarted = true;

                    if (!process.HasExited)
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        await process.WaitForExitAsync();
                    }

                    if (process.HasExited)
                    {
                        if (DoNotCheckErrorsOnSpecificEmulators(selectedEmulatorName, resolvedEmulatorExePath, process,
                                psi, output, error))
                        {
                            return;
                        }

                        await CheckForMemoryAccessViolationAsync(process, psi, output, error);
                        await CheckForDepViolationAsync(process, psi, output, error, selectedEmulatorManager);
                        await CheckForExitCodeWithErrorAnyAsync(process, psi, output, error, selectedEmulatorManager,
                            selectedSystemManager);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Notify developer
                    const string contextMessage = "InvalidOperationException while launching emulator.";
                    _logger.Error(ex, contextMessage);
                    _logger.Debug($"[LaunchRegularEmulatorAsync] Error: {contextMessage}");

                    if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                    {
                        // Notify user
                        await _messageBoxLibrary.InvalidOperationExceptionMessageBoxAsync(
                            PathHelper.ResolveLogFilePath(_configuration));

                        // Offer AI parameter fix
                        await AskAiToFixParameters.ExecuteAsync(selectedSystemManager, selectedEmulatorManager,
                            _messageBoxLibrary, _parameterResolverService, _configuration, _serviceProvider, _logger);
                    }
                }
                catch (Win32Exception ex) // Catch Win32Exception specifically
                {
                    if (CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex))
                    {
                        await _messageBoxLibrary.ApplicationControlPolicyBlockedMessageBoxAsync();
                        // Expected user-environment condition (OS policy blocks the emulator): not a bug.
                        _logger.Information(ex, "Application control policy blocked launching emulator");
                    }
                    else if (CheckApplicationControlPolicyService.IsElevationRequired(ex))
                    {
                        await _messageBoxLibrary.ElevationRequiredMessageBoxAsync();
                        // Expected user-environment condition (the emulator requires admin rights): not a bug.
                        _logger.Information(ex, "Elevation required to launch emulator");
                    }
                    else if (CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex))
                    {
                        // User canceled the operation (e.g., clicked Cancel on UAC prompt) - do nothing, don't log
                    }
                    else if (CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex))
                    {
                        // Expected user-error condition (the emulator is not a valid application for this OS
                        // platform, e.g. a non-Win32 or wrong-architecture binary): not a bug, keep it out of
                        // the bug report service, and don't offer the AI parameter fix.
                        _logger.Information(ex,
                            "Invalid executable format: the emulator is not a valid application for this OS platform. Emulator: {EmulatorPath}",
                            psi.FileName);
                        await _messageBoxLibrary.InvalidExecutableFileMessageBoxAsync();
                    }
                    else
                    {
                        // Existing error handling for other Win32Exceptions
                        // Notify developer
                        // Safely check if the process ever started before trying to access its properties.
                        // A simple way is to check if an ID was ever assigned.
                        string exitCodeInfo;
                        try
                        {
                            // This check is safe even if the process didn't start.
                            _ = process.Id;
                            exitCodeInfo =
                                $"Exit code: {(process.HasExited ? process.ExitCode.ToString(CultureInfo.InvariantCulture) : "N/A (Still Running or Failed to get code)")}";
                        }
                        catch (InvalidOperationException)
                        {
                            exitCodeInfo = "Exit code: N/A (Process failed to start)";
                        }

                        var errorDetail = $"{exitCodeInfo}\n" +
                                          $"Emulator: {psi.FileName}\n" +
                                          $"Calling parameters: {psi.Arguments}\n" +
                                          $"Emulator output: {output}\n" +
                                          $"Emulator error: {error}\n";
                        var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                            ? "User was notified."
                            : "User was not notified.";
                        var contextMessage =
                            $"The emulator could not open the game with the provided parameters. {userNotified}\n\n{errorDetail}";
                        _logger.Error(ex, contextMessage);

                        if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                        {
                            // Notify user
                            await _messageBoxLibrary.CouldNotLaunchGameMessageBoxAsync(
                                PathHelper.ResolveLogFilePath(_configuration));

                            // Offer AI parameter fix
                            await AskAiToFixParameters.ExecuteAsync(selectedSystemManager, selectedEmulatorManager,
                                _messageBoxLibrary, _parameterResolverService, _configuration, _serviceProvider,
                                _logger);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Notify developer
                    // Safely check if the process ever started before trying to access its properties.
                    // A simple way is to check if an ID was ever assigned.
                    string exitCodeInfo;
                    try
                    {
                        // This check is safe even if the process didn't start.
                        _ = process.Id;
                        exitCodeInfo =
                            $"Exit code: {(process.HasExited ? process.ExitCode.ToString(CultureInfo.InvariantCulture) : "N/A (Still Running or Failed to get code)")}";
                    }
                    catch (InvalidOperationException)
                    {
                        exitCodeInfo = "Exit code: N/A (Process failed to start)";
                    }

                    var errorDetail = $"{exitCodeInfo}\n" +
                                      $"Emulator: {psi.FileName}\n" +
                                      $"Calling parameters: {psi.Arguments}\n" +
                                      $"Emulator output: {output}\n" +
                                      $"Emulator error: {error}\n";
                    var userNotified = selectedEmulatorManager.ReceiveANotificationOnEmulatorError
                        ? "User was notified."
                        : "User was not notified.";
                    var contextMessage =
                        $"The emulator could not open the game with the provided parameters. {userNotified}\n\n{errorDetail}";
                    _logger.Error(ex, contextMessage);

                    if (selectedEmulatorManager.ReceiveANotificationOnEmulatorError)
                    {
                        // Notify user
                        await _messageBoxLibrary.CouldNotLaunchGameMessageBoxAsync(
                            PathHelper.ResolveLogFilePath(_configuration));

                        // Offer AI parameter fix
                        await AskAiToFixParameters.ExecuteAsync(selectedSystemManager, selectedEmulatorManager,
                            _messageBoxLibrary, _parameterResolverService, _configuration, _serviceProvider, _logger);
                    }
                }
            }

            // Only attempt to delete if a temporary extraction path was actually set
            if (!string.IsNullOrEmpty(tempExtractionPath) && Directory.Exists(tempExtractionPath))
            {
                try
                {
                    _logger.Debug(
                        $"[LaunchRegularEmulatorAsync] Attempting to delete temporary extraction directory: {tempExtractionPath}");
                    Directory.Delete(tempExtractionPath, true); // Use Directory.Delete with recursive=true
                    _logger.Debug(
                        $"[LaunchRegularEmulatorAsync] Successfully deleted temporary extraction directory: {tempExtractionPath}");
                }
                catch (Exception ex)
                {
                    // Log the error but don't prevent other cleanup actions
                    _logger.Error(ex, $"Failed to delete temporary extraction directory: {tempExtractionPath}");
                    _logger.Debug(
                        $"[LaunchRegularEmulatorAsync] Error deleting temporary extraction directory {tempExtractionPath}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    ///     Occurs after a game has been played and its play history has been updated.
    /// </summary>
    public event EventHandler<GamePlayedEventArgs>? GamePlayed;

    /// <summary>
    ///     Handles the game button click by building the launch context, validating it,
    ///     running configuration handlers, and executing the appropriate launch strategy.
    /// </summary>
    /// <param name="filePath">The path to the game file or directory being launched.</param>
    /// <param name="selectedEmulatorName">The name of the selected emulator.</param>
    /// <param name="selectedSystemName">The name of the system the game belongs to.</param>
    /// <param name="selectedSystemManager">The system manager service for the selected system.</param>
    /// <param name="settings">The application settings manager.</param>
    /// <param name="windowContext">The window context for UI interactions.</param>
    /// <param name="gamePadController">The gamepad controller to pause while the game is running.</param>
    /// <param name="loadingStateProvider">Optional loading state provider for UI feedback.</param>
    internal async Task HandleButtonClickAsync(string filePath,
        string selectedEmulatorName,
        string selectedSystemName,
        ISystemManager selectedSystemManager,
        SettingsManagerService settings,
        IWindowContext windowContext,
        GamePadController gamePadController,
        ILoadingState? loadingStateProvider)
    {
        // 1. Create Context
        var context = new LaunchContext
        {
            FilePath = filePath,
            ResolvedFilePath = PathHelper.ResolveRelativeToAppDirectory(filePath) ?? filePath,
            EmulatorName = selectedEmulatorName,
            SystemName = selectedSystemName,
            SystemManagerService = selectedSystemManager,
            Settings = settings,
            WindowContext = windowContext,
            LoadingState = loadingStateProvider
        };

        // Reset the launch marker for this pipeline run: only a strategy/launcher that really
        // starts the game process may set it (see ReportGameLaunchStarted).
        _gameLaunchStarted = false;

        try
        {
            // 2. Validate SystemManagerService and Emulators before resolving
            if (context.SystemManagerService == null)
            {
                var contextMessage = "SystemManagerService is null when attempting to launch.\n" +
                                     $"SystemName: '{context.SystemName}', EmulatorName: '{context.EmulatorName}', FilePath: '{context.FilePath}'";
                // Expected user-error condition (missing/misconfigured system): not a bug.
                _logger.Information(contextMessage);
                await _messageBoxLibrary.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            if (context.SystemManagerService.Emulators == null || context.SystemManagerService.Emulators.Count == 0)
            {
                var contextMessage =
                    $"SystemManagerService.Emulators is null or empty for system '{context.SystemName}'.\n" +
                    $"EmulatorName: '{context.EmulatorName}', FilePath: '{context.FilePath}'";
                // Expected user-error condition (misconfigured system with no emulators): not a bug.
                _logger.Information(contextMessage);
                await _messageBoxLibrary.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            // 3. Resolve Emulator Manager
            context.EmulatorManager = context.SystemManagerService.Emulators.FirstOrDefault(e =>
                e.EmulatorName.Equals(context.EmulatorName, StringComparison.OrdinalIgnoreCase)) as Emulator;
            if (context.EmulatorManager == null)
            {
                await _messageBoxLibrary.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                // Expected user-error condition (emulator not configured for this system): not a bug.
                _logger.Information(
                    $"Could not find EmulatorManager for emulator '{context.EmulatorName}' in system '{context.SystemName}'.");
                return;
            }

            // 4. Perform Validation
            if (!await ValidateContextAsync(context)) return;

            // 5. Set Parameters
            context.Parameters = context.EmulatorManager.EmulatorParameters;

            // 6. Run Configuration Handlers (Interceptors)
            var handler = _configHandlers.FirstOrDefault(h =>
                h.IsMatch(context.EmulatorName, context.EmulatorManager.EmulatorLocation));
            if (handler != null)
            {
                if (!await handler.HandleConfigurationAsync(context))
                    return;
            }

            // 7. Pre-launch UI/State
            var wasGamePadRunning = gamePadController.IsRunning;
            if (wasGamePadRunning) await gamePadController.StopAsync();

            var startTime = DateTime.Now;
            context.LoadingState?.SetLoadingState(true);

            try
            {
                // 8. Execute Strategy
                var strategy = _launchStrategies.FirstOrDefault(s => s.IsMatch(context));
                if (strategy == null)
                {
                    var errorMessage =
                        $"No launch strategy found for the context: SystemName='{context.SystemName}', EmulatorName='{context.EmulatorName}', FilePath='{context.FilePath}'";
                    _logger.Warning(errorMessage);
                    await _messageBoxLibrary.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                        PathHelper.ResolveLogFilePath(_configuration));
                    return;
                }

                await strategy.ExecuteAsync(context, this);
            }
            finally
            {
                // 9. Post-launch Cleanup & Stats
                context.LoadingState?.SetLoadingState(false);
                if (wasGamePadRunning) await gamePadController.StartAsync();

                var playTime = DateTime.Now - startTime;
                if (_gameLaunchStarted && playTime.TotalSeconds > 5)
                {
                    UpdateStatsAndPlayCountAsync(playTime, context);
                    GamePlayed?.Invoke(this, new GamePlayedEventArgs(context.FilePath, context.SystemName));
                }
            }
        }
        catch (Exception ex)
        {
            var detailedMessage = "Launch Pipeline Failed.\n" +
                                  $"Exception Type: {ex.GetType().FullName}\n" +
                                  $"SystemName: '{context.SystemName ?? "null"}'\n" +
                                  $"EmulatorName: '{context.EmulatorName ?? "null"}'\n" +
                                  $"FilePath: '{context.FilePath ?? "null"}'\n" +
                                  $"ResolvedFilePath: '{context.ResolvedFilePath ?? "null"}'\n" +
                                  $"SystemManagerService is null: {context.SystemManagerService == null}\n" +
                                  $"EmulatorManager is null: {context.EmulatorManager == null}\n" +
                                  $"SystemManagerService.Emulators is null: {context.SystemManagerService?.Emulators == null}\n" +
                                  $"Stack Trace: {ex.StackTrace}";
            _logger.Error(ex, detailedMessage);
            await _messageBoxLibrary.CouldNotLaunchGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    private async Task<bool> ValidateContextAsync(LaunchContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ResolvedFilePath))
        {
            _logger.Warning("Resolved file path is empty");
            await _messageBoxLibrary.FilePathIsInvalidMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
            return false;
        }

        var standardPath = context.ResolvedFilePath;
        var longPath = PathHelper.GetLongPath(standardPath);

        // Check both standard and long path formats for maximum compatibility
        var standardFileExists = File.Exists(standardPath);
        var longFileExists = File.Exists(longPath);
        var standardDirExists = Directory.Exists(standardPath);
        var longDirExists = Directory.Exists(longPath);

        var fileExists = standardFileExists || longFileExists;
        var directoryExists = standardDirExists || longDirExists;

        // If file doesn't exist, try Unicode normalization variations
        // This handles cases where filenames have different normalization forms (NFC vs NFD)
        // commonly occurring when files are created on different operating systems (macOS vs Windows)
        string? normalizedPath = null;
        if (!fileExists && !directoryExists)
        {
            normalizedPath = PathHelper.TryFindFileWithNormalizedPath(standardPath);
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                fileExists = true;
                // Update the resolved path to use the normalized version that actually exists
                context.ResolvedFilePath = normalizedPath;
                _logger.Debug($"[ValidateContextAsync] Found file using Unicode normalization: {normalizedPath}");
            }
        }

        if (!fileExists && !directoryExists)
        {
            var msg = $"File not found: {context.ResolvedFilePath}";

            // Check if the path is in a OneDrive folder and provide specific guidance
            if (!string.IsNullOrEmpty(context.ResolvedFilePath) &&
                context.ResolvedFilePath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                var parentDir = Path.GetDirectoryName(context.ResolvedFilePath);
                var oneDriveFolderExists = !string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir);
                if (!oneDriveFolderExists)
                {
                    msg += "\nThe parent OneDrive folder does not exist or is not accessible. " +
                           "Ensure OneDrive is signed in and synced, and that the folder is available on this device.";
                }
                else
                {
                    msg += "\nThe file is in a OneDrive folder but could not be found. " +
                           "Ensure the file is synced and downloaded to your device. " +
                           "Right-click the file in File Explorer and select 'Always keep on this device'.";
                }
            }

            // Expected condition: the game entry is stale (file deleted/moved since the list was
            // loaded) and the user is already notified via the message box below — not a bug report.
            _logger.Information(new FileNotFoundException(msg), msg);
            await _messageBoxLibrary.FilePathIsInvalidMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
            return false;
        }

        // Detect path format mismatch (exists in one format but not the other)
        // This helps identify Unicode normalization or path handling issues
        var hasFileMismatch = standardFileExists != longFileExists;
        var hasDirMismatch = standardDirExists != longDirExists;

        if (hasFileMismatch || hasDirMismatch)
        {
            var mismatchDetails = "Path validation mismatch detected:\n" +
                                  $"  Original Path: {context.FilePath}\n" +
                                  $"  Resolved Path: {standardPath}\n" +
                                  $"  Long Path: {longPath}\n" +
                                  $"  Normalized Path Found: {normalizedPath ?? "N/A"}\n" +
                                  $"  Standard File.Exists: {standardFileExists}\n" +
                                  $"  Long Path File.Exists: {longFileExists}\n" +
                                  $"  Standard Directory.Exists: {standardDirExists}\n" +
                                  $"  Long Path Directory.Exists: {longDirExists}\n" +
                                  "  This may indicate a Unicode normalization or path handling issue.";

            _logger.Debug(mismatchDetails);

            // Send to developer for investigation but don't block the launch
            _logger.Error(new InvalidOperationException("Path validation mismatch"), mismatchDetails);
        }

        if (string.IsNullOrWhiteSpace(context.EmulatorName))
        {
            _logger.Warning("Emulator name is empty");
            await _messageBoxLibrary.CouldNotLaunchGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
            return false;
        }

        // Add the GroupByFolder check
        if (context.SystemManagerService is { GroupByFolder: true })
        {
            var emulatorName = context.EmulatorName;
            var emulatorLocation = context.EmulatorManager?.EmulatorLocation ?? "";

            var isMame = emulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
                         emulatorLocation.Contains("mame.exe", StringComparison.OrdinalIgnoreCase) ||
                         emulatorLocation.Contains("mame64.exe", StringComparison.OrdinalIgnoreCase);

            var isDosBox = emulatorName.Contains("DOSBox", StringComparison.OrdinalIgnoreCase) ||
                           emulatorLocation.Contains("dosbox", StringComparison.OrdinalIgnoreCase);

            if (!isMame && !isDosBox)
            {
                await _messageBoxLibrary.GroupByFolderOnlyForMameAndDosBoxMessageBoxAsync();
                return false;
            }
        }

        return true;
    }

    private void UpdateStatsAndPlayCountAsync(TimeSpan playTime, LaunchContext context)
    {
        if (context.Settings != null)
        {
            context.Settings.UpdateSystemPlayTime(context.SystemName, playTime);
            context.Settings.SaveAsync();
        }

        var playTimeFormatted = playTime.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
        var playTimeLabel = Application.Current.TryFindResource("Playtime") as string ?? "Playtime";

        _toastNotificationService.ShowToast("Simple Launcher", $"{playTimeLabel}: {playTimeFormatted}");
        _updateStatusBar.UpdateContent("");

        try
        {
            // Use FilePath (original archive path) instead of ResolvedFilePath (temp extracted path)
            // to ensure play history stores the persistent archive location, not the temp file
            _playHistoryManager.AddOrUpdatePlayHistoryItem(context.FilePath, context.SystemName, playTime);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating play history");
        }

        _ = _stats.CallApiAsync(context.EmulatorName);
    }

    private void LogBatchFileContentsOnError(string batchFilePath)
    {
        try
        {
            if (File.Exists(batchFilePath))
            {
                var contents = File.ReadAllText(batchFilePath);
                _logger.Debug($"Batch file contents for '{batchFilePath}':\n{contents}");
                _logger.Debug("End of batch file contents");
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Could not read batch file contents for logging: {ex.Message}");
        }
    }

    private async Task CheckForExitCodeWithErrorAnyAsync(Process process, ProcessStartInfo psi, StringBuilder output,
        StringBuilder error, Emulator emulatorManager, ISystemManager systemManager)
    {
        var userNotified = emulatorManager.ReceiveANotificationOnEmulatorError
            ? "User was notified."
            : "User was not notified.";
        var contextMessage = "The emulator could not open the game with the provided parameters.\n" +
                             $"{userNotified}\n\n" +
                             $"Exit code: {process.ExitCode}\n" +
                             $"Emulator: {psi.FileName}\n" +
                             $"Calling parameters: {psi.Arguments}\n" +
                             $"Emulator output: {output}\n" +
                             $"Emulator error: {error}\n";

        // Ignore MemoryAccessViolation and DepViolation
        if (!process.HasExited || process.ExitCode == 0 || process.ExitCode == MemoryAccessViolation ||
            process.ExitCode == DepViolation)
        {
            return;
        }

        // Handle common RetroArch error that should be ignored
        if (output.ToString().Contains("File open/read error", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug(
                $"[CheckForExitCodeWithErrorAnyAsync] Ignored exit code {process.ExitCode} due to 'File open/read error' in output.");
            return;
        }

        // Handle RetroArch failing due to special characters in path (mkdir permission denied)
        if ((emulatorManager.EmulatorName.Contains("retroarch", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("retroarch", StringComparison.OrdinalIgnoreCase)) &&
            output.ToString().Contains("mkdir(", StringComparison.OrdinalIgnoreCase) &&
            output.ToString().Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug(
                "[CheckForExitCodeWithErrorAnyAsync] RetroArch mkdir permission denied due to special characters in path");
            _logger.Warning(contextMessage);

            if (emulatorManager.ReceiveANotificationOnEmulatorError)
            {
                await _messageBoxLibrary.RetroArchSpecialCharactersInPathMessageBoxAsync();
                await _messageBoxLibrary.WouldYouLikeToOpenTheLogMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));

                // Offer AI parameter fix
                await AskAiToFixParameters.ExecuteAsync(systemManager, emulatorManager, _messageBoxLibrary,
                    _parameterResolverService, _configuration, _serviceProvider, _logger);
            }

            return;
        }

        // Handle RetroArch parameter issues
        if (emulatorManager.EmulatorName.Contains("retroarch", StringComparison.OrdinalIgnoreCase) ||
            emulatorManager.EmulatorLocation.Contains("retroarch", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug("[CheckForExitCodeWithErrorAnyAsync] RetroArch parameter issues");
            _logger.Warning(contextMessage);

            if (emulatorManager.ReceiveANotificationOnEmulatorError)
            {
                await _messageBoxLibrary.RetroArchParameterIssueMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));

                // Offer AI parameter fix
                await AskAiToFixParameters.ExecuteAsync(systemManager, emulatorManager, _messageBoxLibrary,
                    _parameterResolverService, _configuration, _serviceProvider, _logger);
            }

            return;
        }

        // Handle MAME Not Found error
        if ((emulatorManager.EmulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("mame", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("mame64", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("retroarch", StringComparison.OrdinalIgnoreCase)) &&
            (output.ToString().Contains("Not Found", StringComparison.OrdinalIgnoreCase) ||
             output.ToString().Contains("WRONG LENGTH", StringComparison.OrdinalIgnoreCase) ||
             output.ToString().Contains("couldn't find ECD", StringComparison.OrdinalIgnoreCase) ||
             output.ToString().Contains("Required files are missing", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.Debug("[CheckForExitCodeWithErrorAnyAsync] MAME ROM set error");
            _logger.Information(contextMessage);

            if (emulatorManager.ReceiveANotificationOnEmulatorError)
            {
                await _messageBoxLibrary.MameRomSetErrorMessageBoxAsync();
                await _messageBoxLibrary.WouldYouLikeToOpenTheLogMessageBoxAsync(PathHelper.ResolveLogFilePath(_configuration));
                await AskAiToFixParameters.ExecuteAsync(systemManager, emulatorManager, _messageBoxLibrary, _parameterResolverService, _configuration, _serviceProvider, _logger);
            }

            return;
        }

        // Handle MAME Unknown system error
        if ((emulatorManager.EmulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorName.Contains("retroarch", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("mame", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("retroarch", StringComparison.OrdinalIgnoreCase)) &&
            (output.ToString().Contains("Unknown system", StringComparison.OrdinalIgnoreCase) ||
             output.ToString().Contains("approximately matches the following", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.Debug("[CheckForExitCodeWithErrorAnyAsync] MAME Unknown system error");
            _logger.Information(contextMessage);

            if (emulatorManager.ReceiveANotificationOnEmulatorError)
            {
                await _messageBoxLibrary.MameUnknownSystemErrorMessageBoxAsync();
                await _messageBoxLibrary.WouldYouLikeToOpenTheLogMessageBoxAsync(PathHelper.ResolveLogFilePath(_configuration));
                await AskAiToFixParameters.ExecuteAsync(systemManager, emulatorManager, _messageBoxLibrary, _parameterResolverService, _configuration, _serviceProvider, _logger);
            }

            return;
        }

        // Handle MAME Unable to load image
        if ((emulatorManager.EmulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("mame", StringComparison.OrdinalIgnoreCase)) &&
            (output.ToString().Contains("Unable to load image", StringComparison.OrdinalIgnoreCase) ||
             output.ToString().Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.Debug("[CheckForExitCodeWithErrorAnyAsync] MAME Unable to load image error");
            _logger.Warning(contextMessage);

            if (emulatorManager.ReceiveANotificationOnEmulatorError)
            {
                await _messageBoxLibrary.MameUnableToLoadImageMessageBoxAsync();
                await _messageBoxLibrary.WouldYouLikeToOpenTheLogMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));

                // Offer AI parameter fix
                await AskAiToFixParameters.ExecuteAsync(systemManager, emulatorManager, _messageBoxLibrary,
                    _parameterResolverService, _configuration, _serviceProvider, _logger);
            }

            return;
        }

        // Handle MAME corrupted INI (unknown option warnings)
        if ((emulatorManager.EmulatorName.Contains("MAME", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("mame", StringComparison.OrdinalIgnoreCase) ||
             emulatorManager.EmulatorLocation.Contains("mame64", StringComparison.OrdinalIgnoreCase)) &&
            error.ToString().Contains("Warning: unknown option in INI", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug(
                "[CheckForExitCodeWithErrorAnyAsync] MAME unknown option in INI detected. Restoring mame.ini from sample");
            var restored = MameConfigurationService.RestoreMameIniFromSample(psi.FileName, _logger);
            if (restored)
                _logger.Debug("[CheckForExitCodeWithErrorAnyAsync] mame.ini restored successfully. User should retry");
            else
                _logger.Debug("[CheckForExitCodeWithErrorAnyAsync] Failed to restore mame.ini from sample");

            return;
        }

        _logger.Debug($"[CheckForExitCodeWithErrorAnyAsync] Exit code {process.ExitCode} detected.");
        _logger.Warning(contextMessage);

        // Generic error handler
        if (emulatorManager.ReceiveANotificationOnEmulatorError)
        {
            await _messageBoxLibrary.CouldNotLaunchGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));

            // Offer AI parameter fix
            await AskAiToFixParameters.ExecuteAsync(systemManager, emulatorManager, _messageBoxLibrary,
                _parameterResolverService, _configuration, _serviceProvider, _logger);
        }
    }

    private Task CheckForMemoryAccessViolationAsync(Process process, ProcessStartInfo psi, StringBuilder output,
        StringBuilder error)
    {
        if (process.HasExited && process.ExitCode != MemoryAccessViolation) return Task.CompletedTask;

        // Notify developer
        var contextMessage = "There was a memory access violation error running the emulator.\n" +
                             "User was not notified.\n\n" +
                             $"Exit code: {process.ExitCode}\n" +
                             $"Emulator: {psi.FileName}\n" +
                             $"Calling parameters: {psi.Arguments}\n" +
                             $"Emulator output: {output}\n" +
                             $"Emulator error: {error}\n";
        _logger.Information(contextMessage);

        return Task.CompletedTask;
    }

    // ReSharper disable once UnusedParameter.Local
    private Task CheckForDepViolationAsync(Process process, ProcessStartInfo psi, StringBuilder output,
        StringBuilder error, Emulator emulatorManager)
    {
        if (process.HasExited && process.ExitCode != DepViolation) return Task.CompletedTask;

        // Notify developer
        var contextMessage = "Data Execution Prevention (DEP) violation error occurred while running the emulator.\n" +
                             "User was not notified.\n" +
                             $"Exit code: {process.ExitCode}\n" +
                             $"Emulator: {psi.FileName}\n" +
                             $"Calling parameters: {psi.Arguments}\n" +
                             $"Emulator output: {output}\n" +
                             $"Emulator error: {error}\n";
        _logger.Information(contextMessage);

        return Task.CompletedTask;
    }

    private bool DoNotCheckErrorsOnSpecificEmulators(string selectedEmulatorName, string resolvedEmulatorExePath,
        Process process, ProcessStartInfo psi, StringBuilder output, StringBuilder error)
    {
        var emulatorsToSkipErrorChecking = _configuration.GetValue<string[]>("EmulatorsToSkipErrorChecking") ??
        [
            "Kega Fusion", "KegaFusion", "Kega", "Fusion", "Fusion.exe", "Project64", "Project 64",
            "Project64.exe", "Emulicious", "Emulicious.exe", "Speccy", "Speccy.exe", "ProSystem.exe", "ProSystem",
            "fMSX.exe", "fMSX"
        ];

        foreach (var emulatorToSkip in emulatorsToSkipErrorChecking)
        {
            if (selectedEmulatorName.Contains(emulatorToSkip, StringComparison.OrdinalIgnoreCase) ||
                resolvedEmulatorExePath.Contains(emulatorToSkip, StringComparison.OrdinalIgnoreCase))
            {
                var contextMessage = $"User just ran {selectedEmulatorName}.\n" +
                                     "'Simple Launcher' do not track error codes for this emulator.\n\n" +
                                     $"Exit code: {process.ExitCode}\n" +
                                     $"Emulator: {psi.FileName}\n" +
                                     $"Calling parameters: {psi.Arguments}\n" +
                                     $"Emulator output: {output}\n" +
                                     $"Emulator error: {error}\n";
                _logger.Information(contextMessage);

                return true;
            }
        }

        return false;
    }

    private bool IsProtocolRegistered(string protocol)
    {
        if (string.IsNullOrEmpty(protocol)) return false;

        try
        {
            // Protocol names are case-insensitive in registry, but typically stored lowercase.
            // Ensure we check for the existence of the protocol key itself.
            using var protocolKey = Registry.ClassesRoot.OpenSubKey(protocol.ToLowerInvariant());
            if (protocolKey == null)
            {
                _logger.Debug(
                    $"[IsProtocolRegistered] Protocol key '{protocol.ToLowerInvariant()}' not found in HKEY_CLASSES_ROOT.");
                return false;
            }

            // A protocol is considered "registered" if it has a command handler defined.
            // This is typically found under shell\open\command.
            using var shellOpenCommandKey = protocolKey.OpenSubKey(@"shell\open\command");
            if (shellOpenCommandKey == null)
            {
                _logger.Debug(
                    $"[IsProtocolRegistered] 'shell\\open\\command' subkey not found for protocol '{protocol.ToLowerInvariant()}'.");
                return false;
            }

            var command = shellOpenCommandKey.GetValue(null) as string; // Default value
            if (string.IsNullOrWhiteSpace(command))
            {
                _logger.Debug(
                    $"[IsProtocolRegistered] Command handler is empty for protocol '{protocol.ToLowerInvariant()}'.");
                return false;
            }

            _logger.Debug(
                $"[IsProtocolRegistered] Protocol '{protocol.ToLowerInvariant()}' is registered with command: '{command}'.");
            return true;
        }
        catch (Exception ex)
        {
            // Log any exceptions during registry access.
            _logger.Error(ex, $"Error checking if protocol '{protocol}' is registered.");
            _logger.Debug($"[IsProtocolRegistered] Error checking protocol '{protocol}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Determines whether the configured emulator location refers to the Ymir/Yumir
    ///     emulator. Matches the executable's file name or its containing folder name
    ///     exactly ("Ymir"/"Yumir") instead of a substring of the whole path, so unrelated
    ///     paths that merely contain "ymir" (e.g. "...\Skymir\...") do not force extraction.
    /// </summary>
    /// <param name="emulatorLocation">The configured emulator executable path (may be a folder).</param>
    /// <returns>True when the location points at (or into) a Ymir/Yumir installation.</returns>
    private static bool IsYmirEmulatorLocation(string emulatorLocation)
    {
        if (string.IsNullOrWhiteSpace(emulatorLocation)) return false;

        var fileName = Path.GetFileNameWithoutExtension(emulatorLocation);
        if (fileName.Equals("Ymir", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Yumir", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parentPath =
            Path.GetDirectoryName(emulatorLocation.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        var parentName = Path.GetFileName(parentPath ?? string.Empty);
        return parentName.Equals("Ymir", StringComparison.OrdinalIgnoreCase) ||
               parentName.Equals("Yumir", StringComparison.OrdinalIgnoreCase);
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the URL")]
    [GeneratedRegex(@"URL=(.+)", RegexOptions.IgnoreCase, "pt-BR")]
    private static partial Regex MyRegex();
}