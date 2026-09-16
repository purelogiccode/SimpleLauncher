using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.GameLauncher.Strategies;

/// <summary>
///     Launch strategy for Commander Genius that extracts compressed game files into the
///     Commander Genius games directory and launches the emulator with the game data path.
///     Port of the WPF CommanderGeniusLaunchStrategy — the "launched with" toast and
///     status-bar feedback are emitted through the launch context's ILaunchFeedback
///     surface when the host UI provides one.
/// </summary>
public partial class CommanderGeniusLaunchStrategy : ILaunchStrategy
{
    private static ILogger _logger = null!;

    private static readonly string[] KeenDataExtensions =
    [
        ".CK1", ".CK2", ".CK3", ".CK4", ".CK5", ".CK6"
    ];

    private readonly IConfiguration _configuration;
    private readonly IExtractionService _extractionService;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly LocalizationService? _localization;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommanderGeniusLaunchStrategy" /> class.
    /// </summary>
    public CommanderGeniusLaunchStrategy(IExtractionService extractionService, IConfiguration configuration,
        IMessageBoxLibraryService messageBox, ILogger logger, LocalizationService? localization = null)
    {
        _extractionService = extractionService;
        _configuration = configuration;
        _messageBox = messageBox;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _localization = localization;
    }

    /// <inheritdoc />
    public int Priority => 20;

    /// <inheritdoc />
    public bool IsMatch(LaunchContext context)
    {
        if (string.IsNullOrEmpty(context.EmulatorName) ||
            string.IsNullOrEmpty(context.ResolvedFilePath))
        {
            return false;
        }

        if (!context.EmulatorName.Contains("Commander Genius", StringComparison.OrdinalIgnoreCase))
            return false;

        var ext = Path.GetExtension(context.ResolvedFilePath).ToUpperInvariant();
        return ext is ".ZIP" or ".7Z" or ".RAR";
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(LaunchContext context, ILauncherService launcher)
    {
        string? extractionDir = null;

        try
        {
            var cgDataPath = GetCommanderGeniusDataPath(context.EmulatorManager?.EmulatorLocation);
            if (string.IsNullOrEmpty(cgDataPath))
            {
                _logger.Debug("[CommanderGeniusLaunchStrategy] Could not resolve CG data path");
                LogErrorAsync("Could not resolve Commander Genius data path.");
                return;
            }

            var zipName = Path.GetFileNameWithoutExtension(context.ResolvedFilePath);
            if (string.IsNullOrEmpty(zipName) || zipName is "." or ".." ||
                zipName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            {
                _logger.Debug(
                    "[CommanderGeniusLaunchStrategy] Refusing to use an invalid archive name as the extraction folder");
                LogErrorAsync($"Invalid archive name: {Path.GetFileName(context.ResolvedFilePath)}");
                return;
            }

            var gamesDir = Path.Combine(cgDataPath, "games");
            extractionDir = Path.Combine(gamesDir, zipName);

            if (Directory.Exists(extractionDir)) await CleanTempFolder.CleanupTempDirectoryAsync(extractionDir);

            var extracted = await _extractionService.ExtractToFolderAsync(
                context.ResolvedFilePath, extractionDir);

            if (!extracted || !Directory.Exists(extractionDir))
            {
                _logger.Debug("[CommanderGeniusLaunchStrategy] Extraction failed or directory not found");
                return;
            }

            FindAndFlattenGameData(extractionDir);

            if (context.EmulatorManager?.EmulatorLocation != null)
            {
                var emulatorLocation =
                    PathHelper.ResolveRelativeToAppDirectory(context.EmulatorManager?.EmulatorLocation);

                if (string.IsNullOrEmpty(emulatorLocation) || !File.Exists(PathHelper.GetLongPath(emulatorLocation)))
                {
                    _logger.Debug("[CommanderGeniusLaunchStrategy] Emulator executable not found");
                    LogErrorAsync($"Emulator executable not found: {emulatorLocation}");
                    await _messageBox.CouldNotLaunchThisGameMessageBoxAsync(
                        PathHelper.ResolveLogFilePath(_configuration));
                    return;
                }

                var arguments = $"dir=\"games/{zipName}\"";

                _logger.Debug("CommanderGeniusLaunchStrategy:\n\n" +
                              $"Program Location: {emulatorLocation}\n" +
                              $"Arguments: {arguments}\n" +
                              $"Working Directory: {cgDataPath}\n" +
                              $"Zip: {context.ResolvedFilePath}");

                var launchedWith = _localization?.GetString("launchedwith", "launched with") ?? "launched with";
                var originalFileName = Path.GetFileNameWithoutExtension(context.FilePath);

                _logger.Information($"{originalFileName} {launchedWith} {context.EmulatorName}");

                // WPF parity (CommanderGeniusLaunchStrategy): "X launched with Y" toast +
                // status-bar text when the host UI provides an ILaunchFeedback surface.
                if (context.LoadingState is ILaunchFeedback launchFeedback)
                {
                    launchFeedback.ShowToast("Simple Launcher",
                        $"{originalFileName} {launchedWith} {context.EmulatorName}");
                    launchFeedback.SetStatusText(
                        $"{originalFileName} {launchedWith} {context.EmulatorName}");
                }

                var psi = new ProcessStartInfo
                {
                    FileName = emulatorLocation,
                    Arguments = arguments,
                    WorkingDirectory = cgDataPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var output = new StringBuilder();
                var error = new StringBuilder();

                using var process = new Process();
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
                    if (!processStarted)
                        throw new InvalidOperationException("Failed to start Commander Genius process.");

                    if (!process.HasExited)
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        await process.WaitForExitAsync();
                    }
                }
                catch (Win32Exception ex)
                {
                    var exitCodeInfo = SafeGetExitCode(process);
                    var errorDetail = "Commander Genius could not start.\n" +
                                      $"{exitCodeInfo}\n" +
                                      $"Emulator: {psi.FileName}\n" +
                                      $"Arguments: {psi.Arguments}\n" +
                                      $"Error: {ex.Message}";
                    _logger.Error(ex, errorDetail);

                    if (context.EmulatorManager?.ReceiveANotificationOnEmulatorError == true)
                    {
                        await _messageBox.CouldNotLaunchGameMessageBoxAsync(
                            PathHelper.ResolveLogFilePath(_configuration));
                    }
                }
                catch (Exception ex)
                {
                    var exitCodeInfo = SafeGetExitCode(process);
                    var errorDetail = "Commander Genius error.\n" +
                                      $"{exitCodeInfo}\n" +
                                      $"Emulator: {psi.FileName}\n" +
                                      $"Arguments: {psi.Arguments}\n" +
                                      $"Output: {output}\n" +
                                      $"Error: {error}\n" +
                                      $"Exception: {ex.Message}";
                    _logger.Error(ex, errorDetail);

                    if (context.EmulatorManager?.ReceiveANotificationOnEmulatorError == true)
                    {
                        await _messageBox.CouldNotLaunchGameMessageBoxAsync(
                            PathHelper.ResolveLogFilePath(_configuration));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"[CommanderGeniusLaunchStrategy] Unexpected error: {ex}");
            LogErrorAsync($"Unexpected error: {ex.Message}\nFile: {context?.FilePath}");
        }
        finally
        {
            if (extractionDir != null && Directory.Exists(extractionDir))
                await CleanTempFolder.CleanupTempDirectoryAsync(extractionDir);
        }
    }

    private static string? GetCommanderGeniusDataPath(string? emulatorLocation = null)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(documentsPath)) return null;

        var cgDataDir = Path.Combine(documentsPath, "Commander Genius");
        var configPath = Path.Combine(cgDataDir, "cgenius.cfg");

        if (File.Exists(configPath))
        {
            var searchPath1 = ReadSearchPathFromConfig(configPath);
            if (!string.IsNullOrEmpty(searchPath1))
            {
                var resolved = ResolveCgPath(searchPath1, emulatorLocation);
                if (!string.IsNullOrEmpty(resolved) && Directory.Exists(resolved))
                {
                    _logger.Debug($"[CommanderGenius] Using SearchPath1 from config: {resolved}");
                    return resolved;
                }

                _logger.Debug(
                    $"[CommanderGenius] SearchPath1 '{searchPath1}' resolved to '{resolved}' but directory does not exist. Falling back to default.");
            }
            else
            {
                _logger.Debug("[CommanderGenius] SearchPath1 not found in config. Falling back to default");
            }
        }
        else
        {
            _logger.Debug(
                $"[CommanderGenius] Config file not found at {configPath}. Commander Genius may not be properly installed.");
        }

        if (Directory.Exists(cgDataDir)) return cgDataDir;

        var altPath = Path.Combine(documentsPath, "My Documents", "Commander Genius");
        if (Directory.Exists(altPath)) return altPath;

        try
        {
            Directory.CreateDirectory(cgDataDir);
        }
        catch
        {
            return null;
        }

        return cgDataDir;
    }

    private static string? ReadSearchPathFromConfig(string configPath)
    {
        try
        {
            var lines = File.ReadAllLines(configPath);
            var inFileHandlingSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    inFileHandlingSection = trimmed.Equals("[FileHandling]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inFileHandlingSection) continue;

                var match = MyRegex().Match(trimmed);
                if (match.Success) return match.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"[CommanderGenius] Error reading config: {ex.Message}");
        }

        return null;
    }

    private static string? ResolveCgPath(string rawPath, string? emulatorLocation)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return null;

        var resolved = rawPath.Trim();

        if (resolved.Contains("${HOME}", StringComparison.OrdinalIgnoreCase))
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            resolved = resolved.Replace("${HOME}", documentsPath, StringComparison.OrdinalIgnoreCase);
        }

        if (resolved.Contains("${BIN}", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(emulatorLocation))
            {
                var binDir = Path.GetDirectoryName(emulatorLocation);
                if (!string.IsNullOrEmpty(binDir))
                    resolved = resolved.Replace("${BIN}", binDir, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                _logger.Debug("[CommanderGenius] BIN variable found but emulator location is unknown");
                return null;
            }
        }

        if (resolved.Equals(".", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(emulatorLocation))
                resolved = Path.GetDirectoryName(emulatorLocation) ?? resolved;
        }

        return Path.GetFullPath(resolved);
    }

    private static void FindAndFlattenGameData(string extractionDir)
    {
        var dirsToScore = new List<string> { extractionDir };
        try
        {
            dirsToScore.AddRange(Directory.EnumerateDirectories(
                extractionDir, "*", SearchOption.AllDirectories));
        }
        catch (Exception ex)
        {
            _logger.Debug($"[CommanderGenius] Error enumerating directories: {ex.Message}");
        }

        var dirScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirsToScore)
        {
            try
            {
                var count = Directory.EnumerateFiles(dir)
                    .Count(static f => KeenDataExtensions.Contains(
                        Path.GetExtension(f).ToUpperInvariant()));
                if (count > 0) dirScores[dir] = count;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[CommanderGenius] Error scanning directory '{dir}': {ex.Message}");
            }
        }

        string bestDir;
        if (dirScores.Count > 0)
        {
            bestDir = dirScores.OrderByDescending(static kvp => kvp.Value).First().Key;
            _logger.Debug(
                $"[CommanderGenius] Game root identified by Keen files: {bestDir} (score: {dirScores[bestDir]})");
        }
        else
        {
            bestDir = ResolveSingleFolderChain(extractionDir);
            if (!string.Equals(bestDir, extractionDir, StringComparison.Ordinal))
                _logger.Debug($"[CommanderGenius] Game root identified by single-folder chain: {bestDir}");
        }

        if (!string.IsNullOrEmpty(bestDir) &&
            !string.Equals(bestDir, extractionDir, StringComparison.OrdinalIgnoreCase))
        {
            MoveDirectoryContentsToRoot(bestDir, extractionDir);
        }

        CleanEmptySubdirectories(extractionDir);
    }

    private static string ResolveSingleFolderChain(string rootDir)
    {
        try
        {
            var subdirs = Directory.GetDirectories(rootDir);
            if (subdirs.Length != 1) return rootDir;

            var current = subdirs[0];
            while (true)
            {
                var nested = Directory.GetDirectories(current);
                if (nested.Length == 0) break;

                if (nested.Length > 1) return current;

                current = nested[0];
            }

            return current;
        }
        catch (Exception ex)
        {
            _logger.Debug($"[CommanderGenius] Error resolving folder chain: {ex.Message}");
            return rootDir;
        }
    }

    private static void MoveDirectoryContentsToRoot(string sourceDir, string targetRoot)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(targetRoot, Path.GetFileName(file));
            try
            {
                if (File.Exists(destFile)) File.Delete(destFile);
                File.Move(file, destFile);
            }
            catch (Exception ex)
            {
                _logger.Debug($"[CommanderGenius] Failed to move file '{file}': {ex.Message}");
            }
        }

        foreach (var subdir in Directory.GetDirectories(sourceDir))
        {
            var destDir = Path.Combine(targetRoot, Path.GetFileName(subdir));
            try
            {
                if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                Directory.Move(subdir, destDir);
            }
            catch (Exception ex)
            {
                _logger.Debug($"[CommanderGenius] Failed to move directory '{subdir}': {ex.Message}");
            }
        }

        try
        {
            Directory.Delete(sourceDir, true);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[CommanderGenius] Failed to delete source directory '{sourceDir}': {ex.Message}");
        }
    }

    private static void CleanEmptySubdirectories(string rootDir)
    {
        try
        {
            foreach (var subdir in Directory.GetDirectories(rootDir))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(subdir).Any()) Directory.Delete(subdir, false);
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"[CommanderGenius] Error cleaning subdirectories: {ex.Message}");
        }
    }

    private static string SafeGetExitCode(Process process)
    {
        try
        {
            return
                $"Exit code: {(process.HasExited ? process.ExitCode.ToString(CultureInfo.InvariantCulture) : "N/A")}";
        }
        catch
        {
            return "Exit code: N/A";
        }
    }

    private static void LogErrorAsync(string message)
    {
        var fullMessage = $"[CommanderGeniusLaunchStrategy] {message}";
        _logger.Warning(fullMessage);
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the SearchPath1 value")]
    [GeneratedRegex(@"^SearchPath1\s*=\s*(.+)$", RegexOptions.IgnoreCase, "pt-BR")]
    private static partial Regex MyRegex();
}