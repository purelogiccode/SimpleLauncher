namespace SimpleLauncher.Core.Services.CheckPaths;

/// <summary>
///     Provides path validation helpers for files, directories, and emulator executables.
/// </summary>
public static class CheckPath
{
    /// <summary>
    ///     Checks if a path is valid and exists as a file or directory.
    ///     Handles absolute paths, relative paths, and paths using the %BASEFOLDER% placeholder.
    /// </summary>
    /// <param name="path">The path string to check.</param>
    /// <returns>True if the path is valid and exists, false otherwise.</returns>
    public static bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var resolvedPath = PathHelper.ResolveRelativeToAppDirectory(path);
            if (string.IsNullOrEmpty(resolvedPath)) return false;

            var pathForCheck = PathHelper.GetLongPath(resolvedPath);

            // Check if the resolved path exists as a file or directory
            return File.Exists(pathForCheck) || Directory.Exists(pathForCheck);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks if a path is valid and exists as an executable file.
    ///     Handles absolute paths, relative paths, and paths using the %BASEFOLDER% placeholder.
    ///     This is stricter than IsValidPath as it only accepts files, not directories.
    /// </summary>
    /// <param name="path">The path string to check.</param>
    /// <returns>True if the path is valid, exists as a file, and has a valid emulator extension; false otherwise.</returns>
    public static bool IsValidEmulatorExecutablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var resolvedPath = PathHelper.ResolveRelativeToAppDirectory(path);
            if (string.IsNullOrEmpty(resolvedPath)) return false;

            var pathForCheck = PathHelper.GetLongPath(resolvedPath);

            // Check if the resolved path exists as a file (not directory) and has a valid emulator extension
            if (!File.Exists(pathForCheck))
                return false;

            var extension = Path.GetExtension(pathForCheck);
            if (string.IsNullOrEmpty(extension))
            {
                // Native emulator binaries are extensionless outside Windows
                // (e.g. /usr/bin/retroarch), where the Avalonia app runs.
                return !OperatingSystem.IsWindows();
            }

            return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}