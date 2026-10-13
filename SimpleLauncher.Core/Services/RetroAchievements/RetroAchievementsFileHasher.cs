using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.RetroAchievements;

/// <summary>
///     Calculates RetroAchievements hashes for game files. All hash computation is
///     delegated to the bundled RetroAchievementsSharp CLI tool
///     (<c>tools\RetroAchievementsSharp\RetroAchievementsSharp.exe</c> on x64,
///     <c>RetroAchievementsSharp_arm64.exe</c> on arm64) — a 1:1 port of the rcheevos
///     hashing engine that produces the exact same hashes as RAHasher. Single files
///     use the legacy positional interface; bulk scans use the <c>scan</c> subcommand
///     with a forced console and JSON manifest output.
/// </summary>
public class RetroAchievementsFileHasher : IRetroAchievementsFileHasher
{
    private const string ToolFolderName = "RetroAchievementsSharp";
    private const string ToolBaseName = "RetroAchievementsSharp";

    /// <summary>Windows command lines are capped at ~32 K characters; stay safely below.</summary>
    private const int MaxBatchCommandLineLength = 30000;

    private static readonly TimeSpan SingleFileTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromMinutes(30);

    private readonly ILogger _logger;
    private readonly IRetroAchievementsSystemMatcher _systemMatcher;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsFileHasher" /> class.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="systemMatcher">The system matcher used to resolve system names to RetroAchievements console IDs.</param>
    public RetroAchievementsFileHasher(ILogger logErrors, IRetroAchievementsSystemMatcher systemMatcher)
    {
        _logger = logErrors;
        _systemMatcher = systemMatcher;
    }

    /// <inheritdoc />
    public async Task<string?> CalculateHashAsync(string filePath, string systemName,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.Information($"[RA File Hasher] File not found for hashing: {filePath}");
            return null;
        }

        // Single source of truth: usable console id in range and hash logic known to the
        // CLI (rejects e.g. "standalone" id 102 and consoles without a FromFile hasher).
        if (!_systemMatcher.IsSystemSupportedForHashing(systemName))
        {
            _logger.Information(
                $"[RA File Hasher] System '{systemName}' is not supported for RetroAchievements hashing. Skipping hashing.");
            return null;
        }

        var systemId = _systemMatcher.GetSystemId(systemName);

        var toolPath = GetToolExecutablePath();
        if (toolPath == null)
        {
            _logger.Warning(
                $"[RA File Hasher] The RetroAchievementsSharp CLI tool could not be found. Skipping hashing of {filePath}.");
            return null;
        }

        // Zip extraction can lose the Unix executable bit (bug 67780: "Permission
        // denied" on Linux). Restore it best-effort before starting the tool.
        EnsureToolExecutable(toolPath);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // The console ID is passed numerically — NULL-group consoles (Oric, TI83,
        // TIC-80, ESCV, DOS, 3DS, …) can only be addressed by numeric id.
        processStartInfo.ArgumentList.Add(systemId.ToString(CultureInfo.InvariantCulture));
        processStartInfo.ArgumentList.Add(filePath);
        ConfigureEmbeddedToolEnvironment(processStartInfo);

        Process? process = null;
        try
        {
            process = new Process { StartInfo = processStartInfo };
            process.Start();

            // Drain both pipes concurrently: discarding them can deadlock once the
            // child fills the pipe buffer while we block in WaitForExitAsync (CORE-04).
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(SingleFileTimeout);
            await process.WaitForExitAsync(timeoutCts.Token);
            await stderrTask;

            var stdout = (await stdoutTask).Trim();
            var hash = ParseHash(stdout);

            if (hash != null)
            {
                _logger.Debug(
                    $"[RA File Hasher] Calculated hash '{hash}' for '{Path.GetFileName(filePath)}' (System: '{systemName}', ID: {systemId}).");
                return hash;
            }

            _logger.Information(
                $"[RA File Hasher] Could not hash '{filePath}' for system '{systemName}' (ID: {systemId}). The file format may be unsupported, or 3DS decryption keys may be missing.");
            return null;
        }
        catch (OperationCanceledException)
        {
            // Expected condition (application shutdown or per-file timeout): not a bug.
            TryKillProcess(process);
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Information(
                    $"[RA File Hasher] Hashing of '{filePath}' was canceled.");
            }
            else
            {
                _logger.Warning(
                    $"[RA File Hasher] Hashing of '{filePath}' timed out after {SingleFileTimeout.TotalMinutes:0} minute(s).");
            }

            return null;
        }
        catch (Exception ex) when (IsPermissionDenied(ex))
        {
            // Expected user-environment condition (the bundled CLI is not executable,
            // e.g. the executable bit was lost): not a bug.
            _logger.Information(ex,
                $"[RA File Hasher] Permission denied running the hashing tool for '{filePath}'. Reinstalling 'Simple Launcher' usually restores the executable permission.");
            TryKillProcess(process);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex,
                $"[RA File Hasher] An exception occurred while hashing {filePath} for system '{systemName}' (ID: {systemId}).");
            TryKillProcess(process);
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> CalculateHashesAsync(
        IReadOnlyCollection<string> filePaths,
        string systemName,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (filePaths == null || filePaths.Count == 0) return result;

        // Single source of truth (same gate as the single-file path above).
        if (!_systemMatcher.IsSystemSupportedForHashing(systemName))
        {
            _logger.Information(
                $"[RA File Hasher] System '{systemName}' is not supported for RetroAchievements hashing. Skipping hashing.");
            return result;
        }

        var systemId = _systemMatcher.GetSystemId(systemName);

        var toolPath = GetToolExecutablePath();
        if (toolPath == null)
        {
            _logger.Warning(
                $"[RA File Hasher] The RetroAchievementsSharp CLI tool could not be found. Skipping hashing of {filePaths.Count} file(s).");
            return result;
        }

        // Zip extraction can lose the Unix executable bit (bug 67780). Restore it
        // best-effort before starting the tool.
        EnsureToolExecutable(toolPath);

        var outputFile = Path.Combine(Path.GetTempPath(), $"SL_RAScan_{Guid.NewGuid():N}.json");

        try
        {
            foreach (var chunk in ChunkPaths(filePaths))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = toolPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                processStartInfo.ArgumentList.Add("scan");
                processStartInfo.ArgumentList.Add("--console");
                processStartInfo.ArgumentList.Add(systemId.ToString(CultureInfo.InvariantCulture));
                processStartInfo.ArgumentList.Add("--format");
                processStartInfo.ArgumentList.Add("json");
                processStartInfo.ArgumentList.Add("--out");
                processStartInfo.ArgumentList.Add(outputFile);
                foreach (var path in chunk) processStartInfo.ArgumentList.Add(path);

                ConfigureEmbeddedToolEnvironment(processStartInfo);

                try
                {
                    Process? process = null;
                    try
                    {
                        process = new Process { StartInfo = processStartInfo };
                        process.Start();
                        // Await (not discard) both pipes: a verbose scan easily exceeds
                        // the pipe buffer and would deadlock WaitForExitAsync (CORE-04).
                        var batchStdoutTask = process.StandardOutput.ReadToEndAsync();
                        var batchStderrTask = process.StandardError.ReadToEndAsync();

                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(BatchTimeout);
                        await process.WaitForExitAsync(timeoutCts.Token);
                        await batchStdoutTask;
                        await batchStderrTask;

                        foreach (var (path, hash) in ReadScanResults(outputFile)) result[path] = hash;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warning(
                            $"[RA File Hasher] Batch hashing for system '{systemName}' timed out after {BatchTimeout.TotalMinutes:0} minute(s).");
                        TryKillProcess(process);
                    }
                    catch (Exception ex) when (IsPermissionDenied(ex))
                    {
                        // Expected user-environment condition (the bundled CLI is not
                        // executable): not a bug.
                        _logger.Information(ex,
                            $"[RA File Hasher] Permission denied running the hashing tool for system '{systemName}'. Reinstalling 'Simple Launcher' usually restores the executable permission.");
                        TryKillProcess(process);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex,
                            $"[RA File Hasher] An exception occurred while batch hashing for system '{systemName}' (ID: {systemId}).");
                        TryKillProcess(process);
                    }
                    finally
                    {
                        process?.Dispose();
                    }
                }
                finally
                {
                    TryDeleteFile(outputFile);
                }
            }
        }
        finally
        {
            TryDeleteFile(outputFile);
        }

        _logger.Debug(
            $"[RA File Hasher] Batch hashing complete: {result.Count}/{filePaths.Count} files hashed for '{systemName}' (ID: {systemId}).");
        return result;
    }

    /// <summary>
    ///     Splits the file list into batches whose combined command line stays below the
    ///     Windows command-line length limit.
    /// </summary>
    private static IEnumerable<IReadOnlyList<string>> ChunkPaths(IReadOnlyCollection<string> filePaths)
    {
        var chunk = new List<string>();
        var length = 0;

        foreach (var path in filePaths)
        {
            var pathLength = path.Length + 3; // quotes and a separator
            if (chunk.Count > 0 && length + pathLength > MaxBatchCommandLineLength)
            {
                yield return chunk;

                chunk = [];
                length = 0;
            }

            chunk.Add(path);
            length += pathLength;
        }

        if (chunk.Count > 0) yield return chunk;
    }

    /// <summary>
    ///     Reads the JSON manifest produced by the CLI <c>scan --format json</c> command
    ///     and maps every hashed file (full path) to its 32-character hash.
    /// </summary>
    private Dictionary<string, string> ReadScanResults(string outputFile)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!File.Exists(outputFile))
            {
                _logger.Debug($"[RA File Hasher] The CLI tool did not produce a scan manifest at '{outputFile}'.");
                return results;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(outputFile));
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (!row.TryGetProperty("path", out var pathElement) ||
                    !row.TryGetProperty("hash", out var hashElement) ||
                    string.IsNullOrEmpty(hashElement.GetString()))
                {
                    continue;
                }

                var path = pathElement.GetString();
                var hash = ParseHash(hashElement.GetString()!);
                if (path != null && hash != null) results[path] = hash;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"[RA File Hasher] Failed to parse the CLI scan manifest '{outputFile}'.");
        }

        return results;
    }

    /// <summary>
    ///     Validates a 32-character lowercase hex hash, returning null for anything else.
    /// </summary>
    private static string? ParseHash(string? stdout)
    {
        if (string.IsNullOrEmpty(stdout)) return null;

        var line = stdout.Trim();
        if (line.Length != 32 || line.IndexOfAny(['?', ' ']) >= 0) return null;

        return line.All(Uri.IsHexDigit) ? line : null;
    }

    /// <summary>
    ///     Resolves the OS/architecture-appropriate CLI executable under
    ///     <c>tools\RetroAchievementsSharp</c>, or null when unavailable for this platform.
    ///     Windows ships <c>RetroAchievementsSharp.exe</c> / <c>_arm64.exe</c>; Linux
    ///     ships the extension-less <c>RetroAchievementsSharp</c> / <c>_arm64</c>.
    /// </summary>
    private static string? GetToolExecutablePath()
    {
        var architecture = RuntimeInformation.ProcessArchitecture;
        var suffix = architecture switch
        {
            Architecture.X64 => "",
            Architecture.Arm64 => "_arm64",
            _ => null
        };

        if (suffix == null) return null;

        var extension = OperatingSystem.IsWindows() ? ".exe" : "";
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", ToolFolderName,
            $"{ToolBaseName}{suffix}{extension}");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    ///     Prepares the child environment for embedding: telemetry (usage stats and
    ///     bug reports) is disabled so background hash runs never hit the tool's API.
    /// </summary>
    private static void ConfigureEmbeddedToolEnvironment(ProcessStartInfo processStartInfo)
    {
        processStartInfo.Environment["RASHARP_STATS_DISABLE"] = "1";
        processStartInfo.Environment["RASHARP_BUGREPORT_DISABLE"] = "1";
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[RA File Hasher] Failed to clean up temporary file '{path}': {ex.Message}");
        }
    }

    private static void TryKillProcess(Process? process)
    {
        try
        {
            if (process is { HasExited: false }) process.Kill(true);
        }
        catch
        {
            // Best effort — the process may already have exited
        }
    }

    /// <summary>
    ///     Restores the Unix executable bit on the bundled CLI tool when it was lost
    ///     (e.g. by zip extraction). Best effort: failures are Debug-only.
    /// </summary>
    private void EnsureToolExecutable(string toolPath)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            var mode = File.GetUnixFileMode(toolPath);
            if (!mode.HasFlag(UnixFileMode.UserExecute))
            {
                File.SetUnixFileMode(toolPath,
                    mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                _logger.Debug($"[RA File Hasher] Restored the executable bit on '{toolPath}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"[RA File Hasher] Could not set the executable bit on '{toolPath}': {ex.Message}");
        }
    }

    /// <summary>
    ///     Whether the exception (or one of its inner exceptions) is an OS-level permission
    ///     denial when starting the tool: an environment issue, not an app bug.
    /// </summary>
    private static bool IsPermissionDenied(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is Win32Exception win32 &&
                (win32.NativeErrorCode == 13 ||
                 win32.Message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (current is UnauthorizedAccessException)
                return true;
        }

        return false;
    }
}
