namespace SimpleLauncher.Core.Services.CheckPaths;

/// <summary>
///     Builds file-dialog filter strings for picking emulator executables. Windows emulators
///     are always *.exe/*.bat, but Linux/macOS binaries are usually extensionless (or shipped
///     as .AppImage/.sh), so a Windows-style extension filter would hide every selectable file
///     even though <see cref="CheckPath.IsValidEmulatorExecutablePath" /> accepts those paths.
/// </summary>
public static class ExecutableFileFilter
{
    /// <summary>
    ///     Filter for the generic "Select Emulator" pickers. Windows restricts the dialog to
    ///     executable files; other platforms show all files because native binaries carry no
    ///     reliable extension there.
    /// </summary>
    public static string ForEmulator()
    {
        return OperatingSystem.IsWindows()
            ? "Executable Files (*.exe;*.bat)|*.exe;*.bat"
            : "All Files|*.*";
    }

    /// <summary>
    ///     Filter for the <c>Inject*ConfigWindow</c> pickers, which prefer the emulator's
    ///     specific Windows executable name. On other platforms that name has no extension,
    ///     so the dialog shows all files.
    /// </summary>
    /// <param name="description">Filter description used on Windows (e.g. "Ares Executable").</param>
    /// <param name="windowsPattern">Specific Windows executable pattern (e.g. "ares.exe").</param>
    public static string ForNamedExecutable(string description, string windowsPattern)
    {
        return OperatingSystem.IsWindows()
            ? $"{description}|{windowsPattern}|All Executables|*.exe"
            : "All Files|*.*";
    }
}
