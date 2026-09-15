using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;

/// <summary>
///     Handles launching DOS games through DOSBox, including extraction of archives, ISO/CHD mounting,
///     and automatic .conf file generation for game executables.
///     COPY of the WPF DosBoxLaunchStrategy — the file-selection dialog is shown with the Avalonia
///     async ShowDialog API (owner window from IWindowContext); the .conf generation is identical.
/// </summary>
public class DosBoxLaunchStrategy : ILaunchStrategy
{
    private static ILogger _logger = null!;

    private static readonly string[] PriorityGameFormats = [".conf", ".bat", ".exe", ".com"];
    private static readonly List<string> ExtractionFormats = ["conf", "bat", "exe", "com"];
    private readonly IConfiguration _configuration;
    private readonly IExtractionService _extractionService;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IMountChdFiles _mountChdFiles;
    private readonly IMountIsoFiles _mountIsoFiles;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DosBoxLaunchStrategy" /> class.
    /// </summary>
    public DosBoxLaunchStrategy(IExtractionService extractionService, IConfiguration configuration,
        IMessageBoxLibraryService messageBox, IMountChdFiles mountChdFiles, IMountIsoFiles mountIsoFiles,
        ILogger logger)
    {
        _extractionService = extractionService;
        _configuration = configuration;
        _messageBox = messageBox;
        _mountChdFiles = mountChdFiles;
        _mountIsoFiles = mountIsoFiles;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int Priority => 25;

    /// <inheritdoc />
    public bool IsMatch(LaunchContext context)
    {
        if (string.IsNullOrEmpty(context.EmulatorName) ||
            string.IsNullOrEmpty(context.ResolvedFilePath))
        {
            return false;
        }

        if (!IsDosBoxEmulator(context))
            return false;

        if (Directory.Exists(context.ResolvedFilePath))
            return true;

        var ext = Path.GetExtension(context.ResolvedFilePath).ToUpperInvariant();
        return ext is ".ZIP" or ".7Z" or ".RAR" or ".ISO" or ".CHD";
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        var ext = Path.GetExtension(context.ResolvedFilePath).ToUpperInvariant();
        switch (ext)
        {
            case ".ISO":
                await ExecuteIsoAsync(context, launcher);
                return;
            case ".CHD":
                await ExecuteChdAsync(context, launcher);
                return;
            default:
            {
                string? tempDir = null;

                try
                {
                    string workingDir;
                    if (Directory.Exists(context.ResolvedFilePath))
                    {
                        workingDir = context.ResolvedFilePath;
                    }
                    else
                    {
                        var (_, extractedDir) = await _extractionService.ExtractToTempAndGetLaunchFileAsync(
                            context.ResolvedFilePath, ExtractionFormats);

                        if (string.IsNullOrEmpty(extractedDir) || !Directory.Exists(extractedDir))
                        {
                            _logger.Debug("[DosBoxLaunchStrategy] Extraction failed or temp directory not created");
                            return;
                        }

                        tempDir = extractedDir;
                        workingDir = extractedDir;
                    }

                    var gameFiles = FindAllGameFiles(workingDir);
                    if (gameFiles.Count == 0)
                    {
                        _logger.Debug($"[DosBoxLaunchStrategy] No game file (conf/bat/exe/com) found in {workingDir}");
                        // Expected user-input condition (archive contains no DOS executable): not a bug.
                        _logger.Information($"No DOS game executable found in: {context.ResolvedFilePath}");
                        await _messageBox.CouldNotFindAFileMessageBoxAsync();
                        return;
                    }

                    string selectedFile;
                    if (gameFiles.Count == 1)
                    {
                        selectedFile = gameFiles[0];
                        _logger.Debug($"[DosBoxLaunchStrategy] Single game file found, auto-selecting: {selectedFile}");
                    }
                    else if (GetOwnerWindow(context) is not { } owner)
                    {
                        // No owner window (e.g. headless/test run): pick the first file
                        selectedFile = gameFiles[0];
                        _logger.Debug(
                            "[DosBoxLaunchStrategy] No owner window available; auto-selecting the first game file");
                    }
                    else
                    {
                        var dialog = App.ServiceProvider.GetRequiredService<DosBoxFileSelectionWindow>();
                        dialog.Initialize(gameFiles, workingDir);
                        await dialog.ShowDialog(owner);

                        if (string.IsNullOrEmpty(dialog.SelectedFilePath))
                        {
                            _logger.Debug("[DosBoxLaunchStrategy] User cancelled file selection");
                            return;
                        }

                        selectedFile = dialog.SelectedFilePath;
                        _logger.Debug($"[DosBoxLaunchStrategy] User selected file: {selectedFile}");
                    }

                    string confPath;
                    if (Path.GetExtension(selectedFile).Equals(".conf", StringComparison.OrdinalIgnoreCase))
                        confPath = selectedFile;
                    else
                        confPath = GenerateTempConf(workingDir, selectedFile);

                    var launchParameters = BuildLaunchParameters(context.Parameters);

                    await launcher.LaunchRegularEmulatorAsync(
                        confPath,
                        context.EmulatorName,
                        context.SystemManagerService!,
                        context.EmulatorManager!,
                        launchParameters,
                        context.WindowContext!,
                        context.LoadingState,
                        context.ResolvedFilePath);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"[DosBoxLaunchStrategy] Error launching DOS game: {context.ResolvedFilePath}");
                    await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                        PathHelper.ResolveLogFilePath(_configuration));
                }
                finally
                {
                    if (tempDir != null) await CleanTempFolder.CleanupTempDirectoryAsync(tempDir);
                }

                break;
            }
        }
    }

    /// <summary>
    ///     Determines whether the specified launch context targets a DOSBox-family emulator.
    /// </summary>
    internal static bool IsDosBoxEmulator(LaunchContext context)
    {
        var name = context.EmulatorName;
        var path = context.EmulatorManager?.EmulatorLocation ?? "";

        return name.Contains("DOSBox", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("DOSBox-X", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("DOSBox Staging", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("dosbox_pure", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("dosbox", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> FindAllGameFiles(string directory)
    {
        var foundFiles = new List<string>();

        foreach (var format in PriorityGameFormats)
        {
            try
            {
                var files = Directory.GetFiles(directory, $"*{format}", SearchOption.AllDirectories);
                foundFiles.AddRange(files);
            }
            catch (Exception ex)
            {
                _logger.Debug($"[DosBoxLaunchStrategy] Error searching for *{format}: {ex.Message}");
            }
        }

        _logger.Debug($"[DosBoxLaunchStrategy] Found {foundFiles.Count} game file(s) in {directory}");
        return foundFiles;
    }

    private static string GenerateTempConf(string gameDir, string executablePath)
    {
        var executableName = Path.GetFileName(executablePath);
        var executableDir = Path.GetDirectoryName(executablePath);
        var confPath = Path.Combine(gameDir, "_simplelauncher_dosbox.conf");

        string confContent;
        if (!string.IsNullOrEmpty(executableDir) &&
            !executableDir.Equals(gameDir, StringComparison.OrdinalIgnoreCase))
        {
            var relativeDir = executableDir.Replace(gameDir, "").TrimStart('\\', '/');
            confContent = string.Join("\r\n",
                "[dosbox]",
                "",
                "[autoexec]",
                "@echo off",
                $"mount c \"{gameDir}\"",
                "c:",
                $"cd {relativeDir}",
                executableName,
                "exit",
                "");
        }
        else
        {
            confContent = string.Join("\r\n",
                "[dosbox]",
                "",
                "[autoexec]",
                "@echo off",
                $"mount c \"{gameDir}\"",
                "c:",
                executableName,
                "exit",
                "");
        }

        File.WriteAllText(confPath, confContent, Encoding.ASCII);
        _logger.Debug($"[DosBoxLaunchStrategy] Generated conf file: {confPath}");

        return confPath;
    }

    private async Task ExecuteIsoAsync(LaunchContext context, ILauncherService launcher)
    {
        string mountPath;
        string selectedFile;

        try
        {
            // 1. Mount ISO via PowerShell to scan for executables
            var driveLetter =
                await _mountIsoFiles.ExecutePowerShellMountCommandAsync(context.ResolvedFilePath, _logger, _messageBox);
            if (string.IsNullOrEmpty(driveLetter))
            {
                _logger.Debug("[DosBoxLaunchStrategy] Failed to mount ISO via PowerShell");
                await _messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
                return;
            }

            mountPath = $"{driveLetter}:\\";
            _logger.Debug($"[DosBoxLaunchStrategy] ISO mounted to {mountPath} for scanning");

            if (!await _mountIsoFiles.WaitForDirectoryToExistAsync(mountPath, 10000, 200, _logger))
            {
                _logger.Debug($"[DosBoxLaunchStrategy] Mount path {mountPath} did not become available.");
                await _messageBox.ThereWasAnErrorMountingTheFileMessageBoxAsync();
                return;
            }

            var gameFiles = FindAllGameFiles(mountPath);
            switch (gameFiles.Count)
            {
                case 0:
                    _logger.Debug(
                        $"[DosBoxLaunchStrategy] No game file (conf/bat/exe/com) found on mounted ISO at {mountPath}");
                    // Expected user-input condition (ISO contains no DOS executable): not a bug.
                    _logger.Information($"No DOS game executable found in ISO: {context.ResolvedFilePath}");
                    await _messageBox.CouldNotFindAFileMessageBoxAsync();
                    return;
                case 1:
                    selectedFile = gameFiles[0];
                    _logger.Debug(
                        $"[DosBoxLaunchStrategy] Single game file found on ISO, auto-selecting: {selectedFile}");
                    break;
                default:
                {
                    var owner = GetOwnerWindow(context);
                    if (owner is null)
                    {
                        // No owner window (e.g. headless/test run): pick the first file
                        selectedFile = gameFiles[0];
                        _logger.Debug(
                            "[DosBoxLaunchStrategy] No owner window available; auto-selecting the first game file on ISO");
                        break;
                    }

                    var dialog = App.ServiceProvider.GetRequiredService<DosBoxFileSelectionWindow>();
                    dialog.Initialize(gameFiles, mountPath);
                    await dialog.ShowDialog(owner);

                    if (string.IsNullOrEmpty(dialog.SelectedFilePath))
                    {
                        _logger.Debug("[DosBoxLaunchStrategy] User cancelled file selection for ISO");
                        return;
                    }

                    selectedFile = dialog.SelectedFilePath;
                    _logger.Debug($"[DosBoxLaunchStrategy] User selected file from ISO: {selectedFile}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[DosBoxLaunchStrategy] Error scanning ISO: {context.ResolvedFilePath}");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
            return;
        }
        finally
        {
            // 2. Dismount PowerShell ISO — no longer needed after scanning
            if (!string.IsNullOrEmpty(context.ResolvedFilePath))
            {
                _logger.Debug(
                    $"[DosBoxLaunchStrategy] Dismounting PowerShell ISO mount after scanning: {context.ResolvedFilePath}");
                await _mountIsoFiles.ExecutePowerShellDismountCommandAsync(context.ResolvedFilePath, _logger,
                    _messageBox);
            }
        }

        // 3. Generate conf that lets DOSBox mount the ISO natively via imgmount
        var confPath = GenerateIsoConf(mountPath, selectedFile, context.ResolvedFilePath);
        var launchParameters = BuildLaunchParameters(context.Parameters);

        await launcher.LaunchRegularEmulatorAsync(
            confPath,
            context.EmulatorName,
            context.SystemManagerService!,
            context.EmulatorManager!,
            launchParameters,
            context.WindowContext!,
            context.LoadingState,
            context.ResolvedFilePath);
    }

    private static string GenerateIsoConf(string mountPath, string executablePath, string isoFilePath)
    {
        var executableName = Path.GetFileName(executablePath);
        var executableDir = Path.GetDirectoryName(executablePath);
        var tempDir = Path.Combine(Path.GetTempPath(), "SimpleLauncher");
        Directory.CreateDirectory(tempDir);
        var confPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}_dosbox_iso.conf");

        string confContent;
        if (!string.IsNullOrEmpty(executableDir) &&
            !executableDir.TrimEnd('\\').Equals(mountPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            var relativeDir = executableDir.Replace(mountPath, "").TrimStart('\\', '/');
            confContent = string.Join("\r\n",
                "[dosbox]",
                "",
                "[autoexec]",
                "@echo off",
                $"imgmount d \"{isoFilePath}\" -t iso",
                "d:",
                $"cd {relativeDir}",
                executableName,
                "exit",
                "");
        }
        else
        {
            confContent = string.Join("\r\n",
                "[dosbox]",
                "",
                "[autoexec]",
                "@echo off",
                $"imgmount d \"{isoFilePath}\" -t iso",
                "d:",
                executableName,
                "exit",
                "");
        }

        File.WriteAllText(confPath, confContent, Encoding.ASCII);
        _logger.Debug($"[DosBoxLaunchStrategy] Generated ISO conf file: {confPath}");

        return confPath;
    }

    private static string BuildLaunchParameters(string parameters)
    {
        var launchParameters = parameters ?? "";
        if (!launchParameters.Contains("-conf", StringComparison.OrdinalIgnoreCase))
        {
            launchParameters = string.IsNullOrWhiteSpace(launchParameters)
                ? "-conf %ROM%"
                : launchParameters.Contains("%ROM%", StringComparison.OrdinalIgnoreCase)
                    ? launchParameters.Replace("%ROM%", "-conf %ROM%", StringComparison.OrdinalIgnoreCase)
                    : $"-conf %ROM% {launchParameters}";
        }

        return launchParameters;
    }

    private async Task ExecuteChdAsync(LaunchContext context, ILauncherService launcher)
    {
        try
        {
            await using var mountedDrive =
                await _mountChdFiles.MountAsync(context.ResolvedFilePath, "iso9660", _logger, _messageBox);

            if (!mountedDrive.IsMounted)
            {
                _logger.Debug("[DosBoxLaunchStrategy] Failed to mount CHD via CHDMounter");
                return;
            }

            var mountPath = mountedDrive.MountedPath;
            _logger.Debug($"[DosBoxLaunchStrategy] CHD mounted at {mountPath} via CHDMounter");

            var gameFiles = FindAllGameFiles(mountPath);
            if (gameFiles.Count == 0)
            {
                _logger.Debug(
                    $"[DosBoxLaunchStrategy] No game file (conf/bat/exe/com) found on mounted CHD at {mountPath}");
                // Expected user-input condition (CHD contains no DOS executable): not a bug.
                _logger.Information($"No DOS game executable found in CHD: {context.ResolvedFilePath}");
                await _messageBox.CouldNotFindAFileMessageBoxAsync();
                return;
            }

            string selectedFile;
            if (gameFiles.Count == 1)
            {
                selectedFile = gameFiles[0];
                _logger.Debug($"[DosBoxLaunchStrategy] Single game file found on CHD, auto-selecting: {selectedFile}");
            }
            else if (GetOwnerWindow(context) is not { } owner)
            {
                // No owner window (e.g. headless/test run): pick the first file
                selectedFile = gameFiles[0];
                _logger.Debug(
                    "[DosBoxLaunchStrategy] No owner window available; auto-selecting the first game file on CHD");
            }
            else
            {
                var dialog = App.ServiceProvider.GetRequiredService<DosBoxFileSelectionWindow>();
                dialog.Initialize(gameFiles, mountPath);
                await dialog.ShowDialog(owner);

                if (string.IsNullOrEmpty(dialog.SelectedFilePath))
                {
                    _logger.Debug("[DosBoxLaunchStrategy] User cancelled file selection for CHD");
                    return;
                }

                selectedFile = dialog.SelectedFilePath;
                _logger.Debug($"[DosBoxLaunchStrategy] User selected file from CHD: {selectedFile}");
            }

            var confPath = GenerateChdConf(mountPath, selectedFile);
            var launchParameters = BuildLaunchParameters(context.Parameters);

            await launcher.LaunchRegularEmulatorAsync(
                confPath,
                context.EmulatorName,
                context.SystemManagerService!,
                context.EmulatorManager!,
                launchParameters,
                context.WindowContext!,
                context.LoadingState,
                context.ResolvedFilePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[DosBoxLaunchStrategy] Error launching CHD: {context.ResolvedFilePath}");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    /// <summary>
    ///     Resolves the window to own the file-selection dialog: the launch context's
    ///     platform window, falling back to the application main window.
    /// </summary>
    private static Window? GetOwnerWindow(LaunchContext context)
    {
        return context.WindowContext?.PlatformWindow as Window
               ?? (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    private static string GenerateChdConf(string mountPath, string executablePath)
    {
        var executableName = Path.GetFileName(executablePath);
        var executableDir = Path.GetDirectoryName(executablePath);
        var driveLetter = mountPath[0];
        var tempDir = Path.Combine(Path.GetTempPath(), "SimpleLauncher");
        Directory.CreateDirectory(tempDir);
        var confPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}_dosbox_chd.conf");

        string confContent;
        if (!string.IsNullOrEmpty(executableDir) &&
            !executableDir.TrimEnd('\\').Equals(mountPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            var relativeDir = executableDir.Replace(mountPath, "").TrimStart('\\', '/');
            confContent = string.Join("\r\n",
                "[dosbox]",
                "",
                "[autoexec]",
                "@echo off",
                $"mount {char.ToLowerInvariant(driveLetter)} \"{mountPath}\" -t cdrom",
                $"{char.ToLowerInvariant(driveLetter)}:",
                $"cd {relativeDir}",
                executableName,
                "exit",
                "");
        }
        else
        {
            confContent = string.Join("\r\n",
                "[dosbox]",
                "",
                "[autoexec]",
                "@echo off",
                $"mount {char.ToLowerInvariant(driveLetter)} \"{mountPath}\" -t cdrom",
                $"{char.ToLowerInvariant(driveLetter)}:",
                executableName,
                "exit",
                "");
        }

        File.WriteAllText(confPath, confContent, Encoding.ASCII);
        _logger.Debug($"[DosBoxLaunchStrategy] Generated CHD conf file: {confPath}");

        return confPath;
    }
}