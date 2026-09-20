using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace SimpleLauncher.Core.Services.CheckPaths;

/// <summary>
///     Provides path resolution, token expansion, and file lookup helpers used across the application.
/// </summary>
public static partial class PathHelper
{
    private const string BaseFolderPlaceholder = "%BASEFOLDER%";

    // Path length limit to prevent potential recursion issues or overflows
    private const int MaxPathLength = 4096;

    private static readonly string[] GameSpecificPlaceholders =
    [
        "%GAME%", "%ROMNAME%", "%ROMFILE%", "$game$", "$romname$", "$romfile$",
        "{game}", "{romname}", "{romfile}"
    ];

    private static readonly string[] KnownParameterFlags =
    [
        "-f", "--fullscreen", "/f", "-window", "-fullscreen", "--window", "-cart",
        "-L", "-g", "-rompath"
    ];

    private static bool IsKnownFlag(string text)
    {
        return KnownParameterFlags.Any(flag =>
            string.Equals(text, flag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Determines whether a resolved path is the base folder itself or lives inside it.
    ///     Handles base folders that carry a trailing directory separator (e.g.
    ///     <c>AppDomain.CurrentDomain.BaseDirectory</c> on Windows).
    /// </summary>
    internal static bool IsPathContainedInBaseFolder(string resolvedPath, string baseFolder)
    {
        if (string.IsNullOrEmpty(resolvedPath) || string.IsNullOrEmpty(baseFolder)) return false;

        var normalizedResolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolvedPath));
        var normalizedBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseFolder));

        if (normalizedResolved.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase)) return true;

        if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
            !normalizedBase.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            normalizedBase += Path.DirectorySeparatorChar;
        }

        return normalizedResolved.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether the given text contains any game-specific placeholder tokens such as %GAME% or %ROMNAME%.
    /// </summary>
    /// <param name="text">The text to inspect.</param>
    /// <returns>True if the text contains a game-specific placeholder, false otherwise.</returns>
    public static bool ContainsGameSpecificPlaceholder(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        return GameSpecificPlaceholders.Any(placeholder =>
            text.Contains(placeholder, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Resolves placeholder tokens in an emulator parameter string, such as %BASEFOLDER%, %SYSTEMFOLDER%, %ROM%, and
    ///     %NAME%.
    /// </summary>
    /// <param name="parameters">The parameter string containing tokens to resolve.</param>
    /// <param name="systemFolders">The list of system folders used to resolve the %SYSTEMFOLDER% token.</param>
    /// <param name="resolvedEmulatorFolderPath">The emulator folder path used to resolve the %EMULATORFOLDER% token.</param>
    /// <param name="resolvedRomPath">The ROM file path used to resolve the %ROM% token.</param>
    /// <param name="romSystemFolder">The ROM system folder used to resolve the %ROMSYSTEMFOLDER% token.</param>
    /// <param name="resolvedRomName">The ROM name used to resolve the %NAME% token.</param>
    /// <returns>The parameter string with all known tokens resolved.</returns>
    public static string ResolveParameterString(
        string parameters,
        IList<string>? systemFolders = null,
        string? resolvedEmulatorFolderPath = null,
        string? resolvedRomPath = null,
        string? romSystemFolder = null,
        string? resolvedRomName = null)
    {
        if (string.IsNullOrWhiteSpace(parameters)) return "";

        var pathTokenRegex = MyRegex();

        var resolvedParameters = pathTokenRegex.Replace(parameters, match =>
        {
            if (match is not { Success: true }) return "";

            var originalToken = match.Value;
            var isQuotedToken = (originalToken.StartsWith('"') && originalToken.EndsWith('"')) ||
                                (originalToken.StartsWith('\'') && originalToken.EndsWith('\''));
            var tokenForLogic = isQuotedToken ? originalToken[1..^1] : originalToken;

            if (ContainsGameSpecificPlaceholder(tokenForLogic) ||
                IsKnownFlag(tokenForLogic) ||
                (!tokenForLogic.Contains("%BASEFOLDER%", StringComparison.OrdinalIgnoreCase) &&
                 !tokenForLogic.Contains("%SYSTEMFOLDER%", StringComparison.OrdinalIgnoreCase) &&
                 !tokenForLogic.Contains("%ROMSYSTEMFOLDER%", StringComparison.OrdinalIgnoreCase) &&
                 !tokenForLogic.Contains("%EMULATORFOLDER%", StringComparison.OrdinalIgnoreCase) &&
                 !tokenForLogic.Contains("%ROM%", StringComparison.OrdinalIgnoreCase) &&
                 !tokenForLogic.Contains("%NAME%", StringComparison.OrdinalIgnoreCase)))
            {
                return originalToken;
            }

            var processedToken = tokenForLogic;

            if (processedToken.Contains("%BASEFOLDER%", StringComparison.OrdinalIgnoreCase))
            {
                processedToken = processedToken.Replace("%BASEFOLDER%",
                    SanitizePathToken(AppDomain.CurrentDomain.BaseDirectory), StringComparison.OrdinalIgnoreCase);
            }

            if (processedToken.Contains("%SYSTEMFOLDER%", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedSystemFolderPaths = "";
                if (systemFolders is { Count: > 0 })
                {
                    var resolvedFolders = systemFolders
                        .Select(ResolveRelativeToAppDirectory)
                        .Where(static resolved => !string.IsNullOrEmpty(resolved))
                        .Select(SanitizePathToken)
                        .ToList();

                    if (resolvedFolders.Count > 0) resolvedSystemFolderPaths = string.Join(";", resolvedFolders);
                }

                processedToken = processedToken.Replace("%SYSTEMFOLDER%", resolvedSystemFolderPaths,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(romSystemFolder) &&
                processedToken.Contains("%ROMSYSTEMFOLDER%", StringComparison.OrdinalIgnoreCase))
            {
                processedToken = processedToken.Replace("%ROMSYSTEMFOLDER%", SanitizePathToken(romSystemFolder),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(resolvedEmulatorFolderPath) &&
                processedToken.Contains("%EMULATORFOLDER%", StringComparison.OrdinalIgnoreCase))
            {
                processedToken = processedToken.Replace("%EMULATORFOLDER%",
                    SanitizePathToken(resolvedEmulatorFolderPath), StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(resolvedRomPath) &&
                processedToken.Contains("%ROM%", StringComparison.OrdinalIgnoreCase))
            {
                processedToken = processedToken.Replace("%ROM%", SanitizePathToken(resolvedRomPath),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(resolvedRomName) &&
                processedToken.Contains("%NAME%", StringComparison.OrdinalIgnoreCase))
            {
                processedToken = processedToken.Replace("%NAME%", resolvedRomName, StringComparison.OrdinalIgnoreCase);
            }

            processedToken = Environment.ExpandEnvironmentVariables(processedToken);

            var finalTokenValue = processedToken;

            if (isQuotedToken)
            {
                if (originalToken.StartsWith('"'))
                    return $"\"{finalTokenValue}\"";

                return $"'{finalTokenValue}'";
            }

            if (finalTokenValue.Contains(' ') && !isQuotedToken && !finalTokenValue.Contains('"') &&
                !finalTokenValue.Contains('\''))
            {
                return $"\"{finalTokenValue}\"";
            }

            return finalTokenValue;
        });

        return resolvedParameters;
    }

    /// <summary>
    ///     Resolves the given path to a full path relative to the application directory, handling the %BASEFOLDER%
    ///     placeholder.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The full resolved path, or null if the path could not be resolved.</returns>
    public static string? ResolveRelativeToAppDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength) return null;

        // Length check: a single quote satisfies both StartsWith and EndsWith, and
        // path[1..^1] on a length-1 string throws ArgumentOutOfRangeException.
        if (path.Length >= 2 &&
            ((path.StartsWith('"') && path.EndsWith('"')) ||
             (path.StartsWith('\'') && path.EndsWith('\''))))
        {
            path = path[1..^1];
        }

        string basePath;
        var remainingPath = path;

        // Inputs anchored at the app directory (placeholder or relative) must resolve
        // inside it; explicitly absolute paths are the caller's responsibility (CORE-17).
        var mustStayInAppDirectory = true;

        if (path.StartsWith(BaseFolderPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            basePath = AppDomain.CurrentDomain.BaseDirectory;
            remainingPath = path[BaseFolderPlaceholder.Length..];

            // Stored configurations use Windows separators after the placeholder
            // ("%BASEFOLDER%\roms\x"). Backslashes are ordinary characters on Unix, so
            // normalize them before trimming, otherwise the path resolves to a single
            // folder literally named "\roms\x". Normalize first so a leading backslash
            // does not survive TrimStart as a rooted path.
            if (!OperatingSystem.IsWindows())
            {
                remainingPath = remainingPath.Replace('\\', Path.DirectorySeparatorChar);
            }

            remainingPath = remainingPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        else if (Path.IsPathRooted(path))
        {
            basePath = "";
            mustStayInAppDirectory = false;
        }
        else
        {
            basePath = AppDomain.CurrentDomain.BaseDirectory;

            // A migrated/hand-edited value like "tools\retroarch\retroarch" uses Windows
            // separators; backslashes are ordinary characters on Unix, so the whole string
            // would resolve to one file name and the emulator is reported missing (LB-13).
            if (!OperatingSystem.IsWindows())
            {
                remainingPath = remainingPath.Replace('\\', Path.DirectorySeparatorChar);
                remainingPath = remainingPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        try
        {
            var combinedPath = Path.Combine(basePath, remainingPath);
            var fullPath = Path.GetFullPath(combinedPath);

            if (mustStayInAppDirectory &&
                !IsPathContainedInBaseFolder(fullPath, AppDomain.CurrentDomain.BaseDirectory))
            {
                Log.Debug(
                    $"[PathHelper] Rejected path '{path}': resolves outside the application directory.");
                return null;
            }

            return fullPath;
        }
        catch (Exception ex)
        {
            Log.Debug($"[PathHelper] Error resolving path '{path}' relative to app directory: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Resolves the full path of the user error log file, matching the exact location where Serilog writes it
    ///     (see the file sink configuration in App.xaml.cs: '%LOCALAPPDATA%\SimpleLauncher\' + LogPath,
    ///     defaulting to 'error_user.log').
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
    ///     Resolves the full path of the user error log file from application configuration,
    ///     matching the exact location where Serilog writes it (see the file sink configuration
    ///     in App startup: '%LOCALAPPDATA%\SimpleLauncher\' + 'LogPath', defaulting to 'error_user.log').
    /// </summary>
    /// <param name="configuration">
    ///     The application configuration to read the 'LogPath' key from, or null to use the default
    ///     'error_user.log'.
    /// </param>
    /// <returns>The absolute path of the log file.</returns>
    public static string ResolveLogFilePath(IConfiguration? configuration)
    {
        return ResolveLogFilePath(configuration?["LogPath"]);
    }

    /// <summary>
    ///     Resolves the actual log file that should be opened for the user.
    /// </summary>
    /// <remarks>
    ///     The configured log file (see <see cref="ResolveLogFilePath(Microsoft.Extensions.Configuration.IConfiguration)" />) uses a fixed base name such as
    ///     <c>error_user.log</c>, but the Serilog sink is configured with a daily rolling interval
    ///     (<c>RollingInterval.Day</c>). As a result the file actually written to disk is date-stamped, e.g.
    ///     <c>error_user20260825.log</c>. Offering the fixed base name to the user therefore always fails with a
    ///     "file not found" error. This helper returns the most recently written matching file so the open action
    ///     targets the real on-disk log file. If no matching file exists yet, the original base path is returned so
    ///     the caller can report that no log file has been created.
    /// </remarks>
    /// <param name="preferredPath">The preferred log path (typically from <see cref="ResolveLogFilePath(Microsoft.Extensions.Configuration.IConfiguration)" />).</param>
    /// <returns>The path of the most recently written matching log file, or <paramref name="preferredPath" /> if none exists.</returns>
    public static string ResolveActualLogFile(string? preferredPath)
    {
        if (string.IsNullOrWhiteSpace(preferredPath)) return preferredPath ?? string.Empty;

        var directory = Path.GetDirectoryName(preferredPath);
        var fileName = Path.GetFileNameWithoutExtension(preferredPath);
        var extension = Path.GetExtension(preferredPath);

        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            var candidate = Directory.EnumerateFiles(directory, $"{fileName}*{extension}")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (candidate != null) return candidate;
        }

        return preferredPath;
    }

    private static string CombineAndResolveRelativeToAppDirectory(string path1, string path2)
    {
        var resolvedPath1 = ResolveRelativeToAppDirectory(path1);

        if (string.IsNullOrEmpty(resolvedPath1)) return "";

        var combinedPath = Path.Combine(resolvedPath1, path2);
        return ResolveRelativeToAppDirectory(combinedPath) ?? "";
    }

    /// <summary>
    ///     Gets the file name of the given path without its extension.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The file name without its extension.</returns>
    public static string GetFileNameWithoutExtension(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    ///     Gets the file name of the given path.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The file name including its extension.</returns>
    public static string GetFileName(string path)
    {
        return Path.GetFileName(path);
    }

    /// <summary>
    ///     Trims trailing directory separators from the given path token value.
    /// </summary>
    /// <param name="pathTokenValue">The path token value to sanitize.</param>
    /// <returns>The sanitized path token value, or an empty string if null or blank.</returns>
    public static string SanitizePathToken(string? pathTokenValue)
    {
        if (string.IsNullOrEmpty(pathTokenValue)) return "";

        return pathTokenValue.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    ///     Converts the given path to a long path format supported by Windows APIs, if it is not already.
    ///     On non-Windows platforms the path is returned untouched: the extended-length prefix is a
    ///     Windows-only concept, and prefixing a POSIX path would turn it into a literal filename that
    ///     does not exist (breaking existence checks, file sizes, file locks and deletions).
    /// </summary>
    /// <param name="path">The path to convert.</param>
    /// <returns>The long path representation of the given path, or null if the path is null.</returns>
    public static string? GetLongPath(string? path)
    {
        if (path == null) return null;

        if (!OperatingSystem.IsWindows()) return path;

        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        // The \\?\ prefix is only valid on fully-qualified absolute paths. Prefixing a
        // relative path (e.g. "images\default.png") produces an invalid path that makes
        // File.Exists return false, so relative paths are returned untouched (CORE-16).
        try
        {
            if (!Path.IsPathFullyQualified(path)) return path;
        }
        catch
        {
            return path;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return @"\\?\UNC\" + path[2..];

        return @"\\?\" + path;
    }

    /// <summary>
    ///     Searches the given system folders for a file matching the given file name and returns its full path.
    /// </summary>
    /// <param name="systemFolders">The list of system folders to search.</param>
    /// <param name="fileName">The name of the file to find.</param>
    /// <returns>The full path of the found file, or null if it was not found.</returns>
    public static string? FindFileInSystemFolders(IList<string>? systemFolders, string? fileName)
    {
        if (systemFolders == null || systemFolders.Count == 0 || string.IsNullOrEmpty(fileName)) return null;

        foreach (var folder in systemFolders)
        {
            var filePath = CombineAndResolveRelativeToAppDirectory(folder, fileName);
            if (string.IsNullOrEmpty(filePath)) continue;

            var longPath = GetLongPath(filePath);

            if (File.Exists(filePath) || File.Exists(longPath)) return filePath;
        }

        // The game browser scans system folders recursively (GetListOfFilesService), so favorites and
        // play-history records can reference files that live inside subfolders of a system folder.
        // Fall back to a recursive search, preserving the direct-hit priority of the loop above.
        foreach (var folder in systemFolders)
        {
            var resolvedFolder = ResolveRelativeToAppDirectory(folder);
            if (string.IsNullOrEmpty(resolvedFolder) || !Directory.Exists(resolvedFolder)) continue;

            try
            {
                var match = Directory
                    .EnumerateFiles(resolvedFolder, fileName, new EnumerationOptions
                    {
                        MatchCasing = MatchCasing.CaseInsensitive,
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true
                    })
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(match)) return match;
            }
            catch (Exception ex)
            {
                Log.Debug(
                    $"[PathHelper] Error searching subfolders of '{resolvedFolder}' for '{fileName}': {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds which of the given system folders contains the given file path.
    /// </summary>
    /// <param name="systemFolders">The list of system folders to check.</param>
    /// <param name="primarySystemFolder">The primary system folder returned when no match is found.</param>
    /// <param name="filePath">The file path to locate within the folders.</param>
    /// <returns>The resolved folder containing the file, or the primary system folder if none contains it.</returns>
    public static string? FindContainingSystemFolder(IList<string>? systemFolders, string? primarySystemFolder,
        string? filePath)
    {
        if (systemFolders == null || systemFolders.Count == 0 || string.IsNullOrEmpty(filePath))
            return primarySystemFolder;

        var normalizedFilePath = Path.GetFullPath(filePath);

        foreach (var folder in systemFolders)
        {
            var resolvedFolder = ResolveRelativeToAppDirectory(folder);
            if (string.IsNullOrEmpty(resolvedFolder)) continue;

            var normalizedFolder = Path.GetFullPath(resolvedFolder);

            if (IsPathContainedInBaseFolder(normalizedFilePath, normalizedFolder)) return resolvedFolder;
        }

        return primarySystemFolder;
    }

    [GeneratedRegex("""
                    "[^"]*"|'[^']*'|\S+
                    """, RegexOptions.None, 1000)]
    private static partial Regex MyRegex();

    /// <summary>
    ///     Resolves the given folder path and returns it if the directory exists.
    /// </summary>
    /// <param name="folderPath">The folder path to resolve and validate.</param>
    /// <returns>The resolved existing directory path, or null if it does not exist.</returns>
    public static string? TryGetExistingDirectory(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return null;

        try
        {
            var resolved = ResolveRelativeToAppDirectory(folderPath);
            if (!string.IsNullOrEmpty(resolved) && Directory.Exists(resolved)) return resolved;
        }
        catch (Exception ex)
        {
            Log.Debug($"[PathHelper] Error resolving '{folderPath}': {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///     Attempts to find a file on disk matching the given path, tolerating Unicode normalization differences in the file
    ///     name.
    /// </summary>
    /// <param name="filePath">The file path to find.</param>
    /// <returns>The path of the matching file or directory, or null if none was found.</returns>
    public static string? TryFindFileWithNormalizedPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        try
        {
            var directoryPath = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directoryPath) || string.IsNullOrEmpty(fileName)) return null;

            if (!Directory.Exists(directoryPath)) return null;

            var normalizedFileNames = new HashSet<string>(StringComparer.Ordinal)
            {
                fileName,
                fileName.Normalize(NormalizationForm.FormC),
                fileName.Normalize(NormalizationForm.FormD),
                fileName.Normalize(NormalizationForm.FormKC),
                fileName.Normalize(NormalizationForm.FormKD)
            };

            foreach (var existingFile in Directory.EnumerateFiles(directoryPath))
            {
                var existingFileName = Path.GetFileName(existingFile);

                foreach (var normalizedFileName in normalizedFileNames)
                {
                    if (existingFileName.Equals(normalizedFileName, StringComparison.OrdinalIgnoreCase))
                        return existingFile;
                }
            }

            foreach (var existingDir in Directory.EnumerateDirectories(directoryPath))
            {
                var existingDirName = Path.GetFileName(existingDir);

                foreach (var normalizedFileName in normalizedFileNames)
                {
                    if (existingDirName.Equals(normalizedFileName, StringComparison.OrdinalIgnoreCase))
                        return existingDir;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[PathHelper] Error during normalization search: {ex.Message}");
        }

        return null;
    }
}