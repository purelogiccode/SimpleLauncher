namespace SimpleLauncher.Avalonia.Tests.TestHelpers;

/// <summary>
///     Shared helper for locating the SimpleLauncher.Avalonia project directory
///     (and its Resources folder) from the test output folder.
/// </summary>
public static class AvaloniaProjectPathHelper
{
    /// <summary>
    ///     Walks up the directory tree from the test output folder to find the
    ///     SimpleLauncher.Avalonia project directory.
    /// </summary>
    /// <returns>The absolute path to the SimpleLauncher.Avalonia project directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the project directory cannot be located.</exception>
    public static string GetAvaloniaProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "SimpleLauncher.Avalonia");
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "SimpleLauncher.Avalonia.csproj")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the SimpleLauncher.Avalonia project directory from the test output folder.");
    }

    /// <summary>
    ///     Returns the path to the shared localization packs folder
    ///     (SimpleLauncher.Core\Localization) used by both the WPF and Avalonia apps.
    /// </summary>
    public static string GetLocalizationResourcesPath()
    {
        var repoRoot = Path.GetDirectoryName(GetAvaloniaProjectPath());
        var candidate = repoRoot == null ? null : Path.Combine(repoRoot, "SimpleLauncher.Core", "Localization");
        if (candidate == null || !Directory.Exists(candidate))
            throw new DirectoryNotFoundException(
                $"Could not locate the shared localization folder from the test output folder: {candidate}");

        return candidate;
    }
}