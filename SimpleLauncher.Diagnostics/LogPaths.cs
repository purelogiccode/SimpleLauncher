namespace SimpleLauncher.Diagnostics;

/// <summary>
///     Resolves the user error log file locations used by every SimpleLauncher executable.
/// </summary>
public static class LogPaths
{
    /// <summary>
    ///     Resolves the full path of the user error log file, matching the exact location where the
    ///     Serilog file sink writes it (`AppDataPaths.SimpleLauncherDataFolder` + configured file name).
    /// </summary>
    /// <param name="logFileName">
    ///     The log file name from configuration (LogPath key), or null to use the default
    ///     'error_user.log'.
    /// </param>
    /// <returns>The absolute path of the log file.</returns>
    public static string ResolveLogFilePath(string? logFileName)
    {
        if (string.IsNullOrWhiteSpace(logFileName)) logFileName = "error_user.log";

        // Config-controlled value: strip any directory components so an absolute path
        // or ".." escape cannot redirect the log file outside the data folder (CORE-18).
        logFileName = Path.GetFileName(logFileName.Trim());
        if (string.IsNullOrWhiteSpace(logFileName)) logFileName = "error_user.log";

        return Path.Combine(
            AppDataPaths.SimpleLauncherDataFolder,
            logFileName);
    }

    /// <summary>
    ///     Resolves the actual log file that should be opened for the user.
    /// </summary>
    /// <remarks>
    ///     The configured log file uses a fixed base name such as <c>error_user.log</c>, but the
    ///     Serilog sink is configured with a daily rolling interval (<c>RollingInterval.Day</c>).
    ///     As a result the file actually written to disk is date-stamped, e.g.
    ///     <c>error_user20260825.log</c>. This helper returns the most recently written date-stamped
    ///     file when one exists, falling back to the base name only when no rolling file has been
    ///     created yet. If no matching file exists, the original base path is returned so the caller
    ///     can report that no log file has been created.
    /// </remarks>
    /// <param name="preferredPath">The preferred log path (typically from <see cref="ResolveLogFilePath(string)" />).</param>
    /// <returns>The path of the most recently written rolling log file, the base file, or <paramref name="preferredPath" /> if none exists.</returns>
    public static string ResolveActualLogFile(string? preferredPath)
    {
        if (string.IsNullOrWhiteSpace(preferredPath)) return preferredPath ?? string.Empty;

        var directory = Path.GetDirectoryName(preferredPath);
        var fileName = Path.GetFileNameWithoutExtension(preferredPath);
        var extension = Path.GetExtension(preferredPath);

        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            var preferredName = Path.GetFileName(preferredPath);
            var candidates = Directory.EnumerateFiles(directory, $"{fileName}*{extension}")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            // Prefer the date-stamped files written by the Serilog rolling sink (for example
            // error_user20260825.log). The base name can also exist because the bug-report sink
            // appends report dumps to it, but that file only contains submitted reports, not the
            // complete user log the diagnostics dialogs promise to open.
            var rollingFile = candidates.FirstOrDefault(candidate =>
                !string.Equals(Path.GetFileName(candidate), preferredName, StringComparison.OrdinalIgnoreCase));
            if (rollingFile != null) return rollingFile;

            if (candidates.Count > 0) return candidates[0];
        }

        return preferredPath;
    }
}
