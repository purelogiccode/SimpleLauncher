namespace SimpleLauncher.Core.Services.CheckPaths;

/// <summary>
///     Detects the common mistake of running the application directly from the temporary
///     folder an archive tool (ZIP/RAR) created instead of extracting it first.
///     Shared by the WPF and Avalonia startups so both variants behave identically.
/// </summary>
public static class TemporaryExtractionGuard
{
    /// <summary>Title used by the startup warning.</summary>
    public const string WarningTitle = "SimpleLauncher";

    /// <summary>Message shown when the application runs from a temporary extraction folder.</summary>
    public const string WarningMessage =
        "Please extract the application first.\n\n" +
        "It looks like you are running SimpleLauncher from inside a ZIP or RAR archive.\n\n" +
        "Please extract the archive to a folder on your computer and run the application from there.";

    /// <summary>
    ///     Returns true when <paramref name="baseDirectory" /> is the temporary folder itself
    ///     or a directory below it. Case-insensitive on Windows, case-sensitive elsewhere.
    /// </summary>
    /// <param name="baseDirectory">The application directory; defaults to <see cref="AppContext.BaseDirectory" />.</param>
    /// <param name="tempDirectory">The temporary directory; defaults to <see cref="Path.GetTempPath" />.</param>
    public static bool IsRunningFromTemporaryExtraction(string? baseDirectory = null, string? tempDirectory = null)
    {
        var applicationDirectory = NormalizeDirectory(baseDirectory ?? AppContext.BaseDirectory);
        var temporaryDirectory = NormalizeDirectory(tempDirectory ?? Path.GetTempPath());
        if (applicationDirectory.Length == 0 || temporaryDirectory.Length == 0) return false;

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return applicationDirectory.StartsWith(temporaryDirectory, comparison);
    }

    /// <summary>
    ///     Resolves a directory to a full path and ensures it ends with the platform
    ///     separator so "C:\Temp" cannot match "C:\TempOther".
    /// </summary>
    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fullPath = path;
        }

        return Path.EndsInDirectorySeparator(fullPath)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }
}
