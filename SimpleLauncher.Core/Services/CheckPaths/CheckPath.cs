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
    ///     On Windows only .exe/.bat/.lnk files are accepted; outside Windows any existing file
    ///     is accepted because emulator binaries are extensionless, AppImages or .sh/.run
    ///     wrappers there, and the OS reports launch failures for non-executables.
    /// </summary>
    /// <param name="path">The path string to check.</param>
    /// <returns>True if the path is valid, exists as a file, and is an acceptable emulator executable; false otherwise.</returns>
    public static bool IsValidEmulatorExecutablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var resolvedPath = PathHelper.ResolveRelativeToAppDirectory(path);
            if (string.IsNullOrEmpty(resolvedPath)) return false;

            var pathForCheck = PathHelper.GetLongPath(resolvedPath);

            // Check if the resolved path exists as a file (not a directory)
            if (!File.Exists(pathForCheck))
                return false;

            if (!OperatingSystem.IsWindows())
            {
                // Native emulator binaries are extensionless (e.g. /usr/bin/retroarch) and Linux
                // distributions ship AppImages, .sh launchers and versioned .run installers; the
                // extension cannot tell whether a file is executable and the execute bit is not
                // reliable on every mount, so accept any existing file.
                return true;
            }

            var extension = Path.GetExtension(pathForCheck);
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