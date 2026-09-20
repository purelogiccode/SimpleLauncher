using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.GameLauncher.Strategies;

/// <summary>
///     Handles launching DOS games through DOSBox, including extraction of archives, ISO/CHD mounting,
///     and automatic .conf file generation for game executables.
/// </summary>
public class DosBoxLaunchStrategy : ILaunchStrategy
{
    private static ILogger _logger = null!;

    private static readonly string[] PriorityGameFormats = [".conf", ".bat", ".exe", ".com"];
    private static readonly List<string> ExtractionFormats = ["conf", "bat", "exe", "com"];

    /// <summary>
    ///     Executable formats looked for inside a disc image. '.conf' is excluded because DOSBox
    ///     cannot open an image-internal conf file with '-conf'; only real mounted files can.
    /// </summary>
    private static readonly string[] ImageGameFormats = [".bat", ".exe", ".com"];

    private readonly IConfiguration _configuration;
    private readonly IDiscConverter _discConverter;
    private readonly IExtractionService _extractionService;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly IMountChdFiles _mountChdFiles;
    private readonly IMountIsoFiles _mountIsoFiles;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DosBoxLaunchStrategy" /> class.
    /// </summary>
    public DosBoxLaunchStrategy(IExtractionService extractionService, IConfiguration configuration,
        IMessageBoxLibraryService messageBox, IMountChdFiles mountChdFiles, IMountIsoFiles mountIsoFiles,
        IDiscConverter discConverter, ILogger logger)
    {
        _extractionService = extractionService;
        _configuration = configuration;
        _messageBox = messageBox;
        _mountChdFiles = mountChdFiles;
        _mountIsoFiles = mountIsoFiles;
        _discConverter = discConverter;
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
                    else
                    {
                        var dialog = App.ServiceProvider.GetRequiredService<DosBoxFileSelectionWindow>();
                        dialog.Initialize(gameFiles, workingDir);
                        var result = dialog.ShowDialog();

                        if (result != true || string.IsNullOrEmpty(dialog.SelectedFilePath))
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
                    var dialog = App.ServiceProvider.GetRequiredService<DosBoxFileSelectionWindow>();
                    dialog.Initialize(gameFiles, mountPath);
                    var result = dialog.ShowDialog();

                    if (result != true || string.IsNullOrEmpty(dialog.SelectedFilePath))
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

        try
        {
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
        finally
        {
            // The conf references the user's ROM paths and is single-use: delete it once
            // DOSBox has exited (or the launch failed) instead of leaking it in %TEMP%.
            TryDeleteConfFile(confPath);
        }
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
        // CHDMounter + Dokan are Windows-only. On Linux/macOS the image is converted with the
        // managed CHDSharp decoder and mounted inside DOSBox with 'imgmount' instead.
        if (!OperatingSystem.IsWindows())
        {
            await ExecuteChdWithConversionAsync(context, launcher);
            return;
        }

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
            else
            {
                var dialog = App.ServiceProvider.GetRequiredService<DosBoxFileSelectionWindow>();
                dialog.Initialize(gameFiles, mountPath);
                var result = dialog.ShowDialog();

                if (result != true || string.IsNullOrEmpty(dialog.SelectedFilePath))
                {
                    _logger.Debug("[DosBoxLaunchStrategy] User cancelled file selection for CHD");
                    return;
                }

                selectedFile = dialog.SelectedFilePath;
                _logger.Debug($"[DosBoxLaunchStrategy] User selected file from CHD: {selectedFile}");
            }

            var confPath = GenerateChdConf(mountPath, selectedFile);
            var launchParameters = BuildLaunchParameters(context.Parameters);

            try
            {
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
            finally
            {
                // The conf references the user's ROM paths and is single-use: delete it once
                // DOSBox has exited (or the launch failed) instead of leaking it in %TEMP%.
                TryDeleteConfFile(confPath);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[DosBoxLaunchStrategy] Error launching CHD: {context.ResolvedFilePath}");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
    }

    /// <summary>
    ///     Non-Windows fallback for CHD images: CHDMounter/Dokan do not exist on Linux/macOS, so
    ///     the image is converted with the managed CHDSharp decoder (CUE/BIN for CDs, ISO for
    ///     DVDs), its files are listed from the cooked ISO9660 data track, and DOSBox mounts the
    ///     converted image itself through 'imgmount' (Windows mounts the same contents through a
    ///     Dokan drive). The converted files are deleted once DOSBox exits.
    /// </summary>
    private async Task ExecuteChdWithConversionAsync(LaunchContext context, ILauncherService launcher)
    {
        var chdPath = context.ResolvedFilePath;
        string? convertedPath = null;
        var imageType = "cdrom"; // imgmount type: a CUE/BIN pair is a CD-ROM, an ISO a plain image

        try
        {
            context.LoadingState?.SetLoadingState(true, "Converting CHD...");
            try
            {
                convertedPath = await _discConverter.ConvertChdToCueBinAsync(chdPath);
                if (convertedPath == null)
                {
                    // DVDs are not CD images; CHDSharp extracts them straight to an ISO.
                    convertedPath = await _discConverter.ConvertChdToIsoAsync(chdPath);
                    imageType = "iso";
                }
            }
            finally
            {
                context.LoadingState?.SetLoadingState(false);
            }

            if (convertedPath == null)
            {
                // Conversion details are logged by the converter (expected user-data condition).
                _logger.Debug($"[DosBoxLaunchStrategy] Could not convert CHD image: {chdPath}");
                await _messageBox.ThereWasAnErrorLaunchingThisGameMessageBoxAsync(
                    PathHelper.ResolveLogFilePath(_configuration));
                return;
            }

            // The cooked CUE/BIN data track and an extracted ISO are both ISO9660 images.
            var listingPath = Path.ChangeExtension(convertedPath, ".bin");
            if (!File.Exists(listingPath)) listingPath = convertedPath;

            var gameFiles = FindGameFilesInImage(listingPath);
            if (gameFiles.Count == 0)
            {
                _logger.Debug($"[DosBoxLaunchStrategy] No game file (bat/exe/com) found in CHD image at {listingPath}");
                // Expected user-input condition (image contains no DOS executable): not a bug.
                _logger.Information($"No DOS game executable found in CHD: {chdPath}");
                await _messageBox.CouldNotFindAFileMessageBoxAsync();
                return;
            }

            var selectedFile = await SelectGameFileFromImageAsync(context, gameFiles);
            if (selectedFile == null) return; // user cancelled

            var confPath = GenerateImageConf(convertedPath, imageType, selectedFile);
            var launchParameters = BuildLaunchParameters(context.Parameters);

            try
            {
                await launcher.LaunchRegularEmulatorAsync(
                    confPath,
                    context.EmulatorName,
                    context.SystemManagerService!,
                    context.EmulatorManager!,
                    launchParameters,
                    context.WindowContext!,
                    context.LoadingState,
                    chdPath);
            }
            finally
            {
                // The conf references the user's ROM paths and is single-use: delete it once
                // DOSBox has exited (or the launch failed) instead of leaking it in %TEMP%.
                TryDeleteConfFile(confPath);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[DosBoxLaunchStrategy] Error launching CHD: {chdPath}");
            await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                PathHelper.ResolveLogFilePath(_configuration));
        }
        finally
        {
            CleanupConvertedImageFiles(convertedPath);
        }
    }

    /// <summary>
    ///     Returns the DOS executables inside a disc image (ordered by format priority), listed
    ///     from the cooked ISO9660 data track with the primary 8.3 names DOSBox itself shows.
    /// </summary>
    private static List<string> FindGameFilesInImage(string imagePath)
    {
        var listedFiles = Iso9660ImageReader.ListFiles(imagePath, _logger);
        var gameFiles = new List<string>();

        foreach (var format in ImageGameFormats)
            gameFiles.AddRange(listedFiles
                .Where(file => file.EndsWith(format, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase));

        _logger.Debug($"[DosBoxLaunchStrategy] Found {gameFiles.Count} game file(s) in image at {imagePath}");
        return gameFiles;
    }

    /// <summary>
    ///     Asks the user to choose between multiple DOS executables found inside a CHD image
    ///     when there is no single candidate. Returns the image-relative path ('/'-separated)
    ///     of the selected file, or null on cancel.
    /// </summary>
    private static Task<string?> SelectGameFileFromImageAsync(LaunchContext context, List<string> gameFiles)
    {
        if (gameFiles.Count == 1)
        {
            _logger.Debug($"[DosBoxLaunchStrategy] Single game file found in CHD, auto-selecting: {gameFiles[0]}");
            return Task.FromResult<string?>(gameFiles[0]);
        }

        // The listing is image-relative; give the dialog a DOS-style root so it can display
        // relative folder names and return the selected image-relative path.
        const string imageRoot = "d:/";
        var dialog = App.ServiceProvider.GetRequiredService<DosBoxFileSelectionWindow>();
        dialog.Initialize(gameFiles.Select(file => imageRoot + file).ToList(), imageRoot);
        var result = dialog.ShowDialog();

        if (result != true || string.IsNullOrEmpty(dialog.SelectedFilePath))
        {
            _logger.Debug("[DosBoxLaunchStrategy] User cancelled file selection for CHD");
            return Task.FromResult<string?>(null);
        }

        _logger.Debug($"[DosBoxLaunchStrategy] User selected file from CHD: {dialog.SelectedFilePath}");
        return Task.FromResult<string?>(dialog.SelectedFilePath.StartsWith(imageRoot, StringComparison.OrdinalIgnoreCase)
            ? dialog.SelectedFilePath[imageRoot.Length..]
            : dialog.SelectedFilePath);
    }

    /// <summary>
    ///     Generates a DOSBox conf that mounts the converted disc image with 'imgmount' and runs
    ///     the selected executable from it.
    /// </summary>
    private static string GenerateImageConf(string imagePath, string imageType, string relativeFilePath)
    {
        var executableName = Path.GetFileName(relativeFilePath);
        var relativeDir = Path.GetDirectoryName(relativeFilePath)?.Replace('/', '\\') ?? "";
        var tempDir = Path.Combine(Path.GetTempPath(), "SimpleLauncher");
        Directory.CreateDirectory(tempDir);
        var confPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}_dosbox_{imageType}.conf");

        var lines = new List<string>
        {
            "[dosbox]",
            "",
            "[autoexec]",
            "@echo off",
            $"imgmount d \"{imagePath}\" -t {imageType}",
            "d:"
        };
        if (!string.IsNullOrEmpty(relativeDir)) lines.Add($"cd {relativeDir}");
        lines.Add(executableName);
        lines.Add("exit");
        lines.Add("");

        File.WriteAllText(confPath, string.Join("\r\n", lines), Encoding.ASCII);
        _logger.Debug($"[DosBoxLaunchStrategy] Generated image conf file: {confPath}");

        return confPath;
    }

    /// <summary>
    ///     Deletes the temporary image files produced by a CHD conversion (the CUE and its BIN
    ///     sibling, or the ISO). Cleanup failures are logged at Debug: the next startup cleanup
    ///     of %TEMP%\SimpleLauncher removes any file left behind.
    /// </summary>
    private static void CleanupConvertedImageFiles(string? convertedPath)
    {
        if (string.IsNullOrEmpty(convertedPath)) return;

        try
        {
            var binPath = Path.ChangeExtension(convertedPath, ".bin");
            if (File.Exists(convertedPath)) File.Delete(convertedPath);
            if (File.Exists(binPath)) File.Delete(binPath);
            _logger.Debug($"[DosBoxLaunchStrategy] Deleted converted CHD image files: {convertedPath}");
        }
        catch (Exception ex)
        {
            _logger.Debug(
                $"[DosBoxLaunchStrategy] Failed to delete converted CHD image files {convertedPath}: {ex.Message}");
        }
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

    /// <summary>
    ///     Deletes a generated DOSBox conf file after use. Cleanup failures are logged at Debug:
    ///     the next startup cleanup of %TEMP%\SimpleLauncher removes any file left behind.
    /// </summary>
    private static void TryDeleteConfFile(string confPath)
    {
        try
        {
            if (File.Exists(confPath)) File.Delete(confPath);
            _logger.Debug($"[DosBoxLaunchStrategy] Deleted temporary conf file: {confPath}");
        }
        catch (Exception ex)
        {
            _logger.Debug($"[DosBoxLaunchStrategy] Failed to delete temporary conf file {confPath}: {ex.Message}");
        }
    }
}