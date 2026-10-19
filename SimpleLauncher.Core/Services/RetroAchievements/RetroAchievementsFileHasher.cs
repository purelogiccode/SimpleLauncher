using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.ExternalToolLauncher;

namespace SimpleLauncher.Core.Services.RetroAchievements;

/// <summary>
///     Calculates RetroAchievements hashes for game files. All hash computation is
///     delegated to the bundled RetroAchievementsSharp CLI tool
///     (<c>tools\RetroAchievementsSharp\&lt;rid&gt;\RetroAchievementsSharp.Cli</c>, with the
///     <c>.exe</c> suffix on Windows) — a 1:1 port of the rcheevos hashing engine that
///     produces the exact same hashes as RAHasher. Hash requests use the tool's legacy
///     positional interface (<c>&lt;system-id&gt; &lt;file&gt;...</c>), which supports many
///     files per invocation and prints one result line per file to stdout. The tool
///     stops at the first file it cannot hash, so the hasher automatically resumes the
///     remaining files in a follow-up invocation.
/// </summary>
public class RetroAchievementsFileHasher : IRetroAchievementsFileHasher
{
    private const string ToolFolderName = "RetroAchievementsSharp";
    private const string ToolBaseName = "RetroAchievementsSharp.Cli";

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
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(SingleFileTimeout);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
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

        foreach (var chunk in ChunkPaths(filePaths))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pending = chunk.ToList();
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = await RunBatchAsync(toolPath, systemId, pending, cancellationToken);
                if (batch == null)
                {
                    // Hard failure (timeout, missing permission, crashed process): the
                    // error was already logged — stop hashing this batch.
                    break;
                }

                var (attempted, hashes) = batch.Value;
                foreach (var (path, hash) in hashes) result[path] = hash;

                if (attempted >= pending.Count) break;

                if (attempted == 0)
                {
                    // Safety valve for a process that produced no result line at all:
                    // skip one file so the retry loop always makes progress.
                    _logger.Information(
                        $"[RA File Hasher] No hash result was produced for '{pending[0]}' ({systemName}); skipping it.");
                    pending.RemoveAt(0);
                    continue;
                }

                // The CLI stops at the first file it cannot hash; retry the untouched
                // tail so one unhashable file never hides the rest of the library.
                _logger.Debug(
                    $"[RA File Hasher] The CLI stopped at '{pending[attempted - 1]}' ({systemName}); hashing the remaining {pending.Count - attempted} file(s).");
                pending = pending.GetRange(attempted, pending.Count - attempted);
            }
        }

        _logger.Debug(
            $"[RA File Hasher] Batch hashing complete: {result.Count}/{filePaths.Count} files hashed for '{systemName}' (ID: {systemId}).");
        return result;
    }

    /// <summary>
    ///     Runs one CLI invocation for the given files (forced console id) and parses
    ///     its stdout. Returns null when the process could not run to completion
    ///     (timeout, permission denial, crash) — the condition is logged.
    /// </summary>
    private async Task<(int Attempted, Dictionary<string, string> Hashes)?> RunBatchAsync(
        string toolPath, int systemId, IReadOnlyList<string> filePaths, CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.ArgumentList.Add(systemId.ToString(CultureInfo.InvariantCulture));
        foreach (var path in filePaths) processStartInfo.ArgumentList.Add(path);
        ConfigureEmbeddedToolEnvironment(processStartInfo);

        Process? process = null;
        try
        {
            process = new Process { StartInfo = processStartInfo };
            process.Start();

            // Drain both pipes concurrently: discarding them can deadlock once the
            // child fills the pipe buffer while we block in WaitForExitAsync (CORE-04).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(BatchTimeout);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);
            await stderrTask;

            return ParseBatchOutput(filePaths, await stdoutTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected condition (application shutdown): not a bug.
            TryKillProcess(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.Warning(
                $"[RA File Hasher] Batch hashing timed out after {BatchTimeout.TotalMinutes:0} minute(s).");
            TryKillProcess(process);
            return null;
        }
        catch (Exception ex) when (IsPermissionDenied(ex))
        {
            // Expected user-environment condition (the bundled CLI is not
            // executable): not a bug.
            _logger.Information(ex,
                "[RA File Hasher] Permission denied running the hashing tool. Reinstalling 'Simple Launcher' usually restores the executable permission.");
            TryKillProcess(process);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[RA File Hasher] An exception occurred while batch hashing.");
            TryKillProcess(process);
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    ///     Parses the CLI stdout of one batch invocation. The tool prints one result line
    ///     per attempted file, in input order: <c>&lt;hash&gt; &lt;file name&gt;</c> for a hashed
    ///     file (a bare <c>&lt;hash&gt;</c> when the invocation has a single file) and
    ///     <c> &lt;file name&gt;</c> (empty hash) when it could not hash the file, after which
    ///     it stops processing the remaining arguments. Other lines (e.g. the
    ///     "Could not open file" diagnostic) are ignored.
    /// </summary>
    private static (int Attempted, Dictionary<string, string> Hashes) ParseBatchOutput(
        IReadOnlyList<string> filePaths, string stdout)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // The CLI prints file names, not full paths: map each result line back to the
        // next not-yet-matched input file with the same name (input order is preserved).
        var pendingByFileName = new Dictionary<string, Queue<int>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < filePaths.Count; i++)
        {
            var fileName = Path.GetFileName(filePaths[i]);
            if (!pendingByFileName.TryGetValue(fileName, out var indexes))
                pendingByFileName[fileName] = indexes = new Queue<int>();
            indexes.Enqueue(i);
        }

        var attempted = 0;
        var remaining = filePaths.Count;
        foreach (var rawLine in stdout.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0) continue;

            // A single-file invocation prints only the bare hash, without the file name.
            if (line.Length == 32)
            {
                var bareHash = ParseHash(line);
                if (bareHash == null || remaining != 1) continue;

                var onlyIndex = pendingByFileName.Values.First(static queue => queue.Count > 0).Dequeue();
                remaining--;
                hashes[filePaths[onlyIndex]] = bareHash;
                if (onlyIndex == attempted) attempted = onlyIndex + 1;
                continue;
            }

            // Success lines are "<32-char hash> <file name>"; failure lines have no hash.
            var hash = line.Length >= 33 && line[32] == ' ' ? ParseHash(line[..32]) : null;
            var fileName = hash != null ? line[33..] : line.TrimStart();

            if (!pendingByFileName.TryGetValue(fileName, out var indexes) || indexes.Count == 0)
                continue; // Diagnostic or unknown line — not a result for any input file

            var index = indexes.Dequeue();
            remaining--;
            if (hash != null) hashes[filePaths[index]] = hash;

            // Contiguous input order: the tool stops at the first file it cannot hash, so
            // the number of attempted files is the failed file's position in the input.
            if (index == attempted) attempted = index + 1;
        }

        return (attempted, hashes);
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
    ///     <c>tools\RetroAchievementsSharp\&lt;rid&gt;</c>, or null when unavailable for this
    ///     platform. The payload ships one folder per RID (win-x64, win-arm64, linux-x64,
    ///     linux-arm64); Windows uses <c>RetroAchievementsSharp.Cli.exe</c>, Linux the
    ///     extension-less <c>RetroAchievementsSharp.Cli</c>.
    /// </summary>
    private static string? GetToolExecutablePath()
    {
        var runtimeIdentifier = ExternalToolLauncherService.GetPlatformRuntimeIdentifier();
        if (runtimeIdentifier == null) return null;

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", ToolFolderName,
            runtimeIdentifier, ExternalToolLauncherService.GetPlatformExecutableName(ToolBaseName));
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