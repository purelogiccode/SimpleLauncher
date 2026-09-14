namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Locates the EBOOT.BIN file (PS3 executable) in a directory, checking common PS3 folder structures first.
/// </summary>
public static class FindEbootBin
{
    private const string TargetFileName = "EBOOT.BIN";

    // Names are matched in managed code with an explicit case-insensitive comparison while
    // inaccessible entries are skipped. This also works on Dokan/WinFsp virtual drives, whose
    // wildcard/casing behaviour can differ from a local filesystem (see bug 67013).
    private static readonly EnumerationOptions TopLevelOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.None,
        MatchCasing = MatchCasing.CaseInsensitive
    };

    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.None,
        MatchCasing = MatchCasing.CaseInsensitive
    };

    /// <summary>
    ///     Recursively searches for EBOOT.BIN, prioritizing the top directory and PS3_GAME/USRDIR structure.
    /// </summary>
    public static string? FindEbootBinRecursive(string directoryPath, ILogger logErrors, ILogger logger)
    {
        if (string.IsNullOrEmpty(directoryPath)) return null;

        logger.Debug($"[FindEbootBin.FindEbootBinRecursive] Searching for {TargetFileName} in {directoryPath}");

        try
        {
            // Check top directory first
            var fileInTopDir = FindFileByName(directoryPath, TopLevelOptions);
            if (fileInTopDir != null)
            {
                logger.Debug(
                    $"[FindEbootBin.FindEbootBinRecursive] Found {TargetFileName} in top directory: {fileInTopDir}");
                return fileInTopDir;
            }

            // Check common PS3 structure: <mount>\PS3_GAME\USRDIR\EBOOT.BIN
            foreach (var ps3GameDir in FindDirectoriesByName(directoryPath, "PS3_GAME", TopLevelOptions))
            {
                foreach (var usrDir in FindDirectoriesByName(ps3GameDir, "USRDIR", TopLevelOptions))
                {
                    var fileInUsrDir = FindFileByName(usrDir, TopLevelOptions);
                    if (fileInUsrDir == null) continue;

                    logger.Debug(
                        $"[FindEbootBin.FindEbootBinRecursive] Found {TargetFileName} in PS3_GAME/USRDIR: {fileInUsrDir}");
                    return fileInUsrDir;
                }
            }

            // Fallback to full recursive search if not found in common locations
            logger.Debug(
                $"[FindEbootBin.FindEbootBinRecursive] {TargetFileName} not found in typical locations. Starting full recursive search in {directoryPath}...");
            var fileFoundRecursively = FindFileByName(directoryPath, RecursiveOptions);
            if (fileFoundRecursively != null)
            {
                logger.Debug(
                    $"[FindEbootBin.FindEbootBinRecursive] Found {TargetFileName} via full recursive search: {fileFoundRecursively}");
                return fileFoundRecursively;
            }
        }
        catch (Exception ex)
        {
            logger.Debug(
                $"[FindEbootBin.FindEbootBinRecursive] Error searching for {TargetFileName} in {directoryPath}: {ex.Message}");

            // Expected condition (mounted/archived volume search failure; the caller reports the
            // missing game file to the user): not a bug, keep it out of the bug report service.
            logErrors.Information(ex, $"Error while searching for EBOOT.BIN in directory at {directoryPath}.");
        }

        logger.Debug($"[FindEbootBin.FindEbootBinRecursive] {TargetFileName} not found in {directoryPath}.");
        return null;
    }

    private static string? FindFileByName(string directoryPath, EnumerationOptions options)
    {
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", options))
        {
            if (string.Equals(Path.GetFileName(file), TargetFileName, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        return null;
    }

    private static IEnumerable<string> FindDirectoriesByName(string directoryPath, string directoryName,
        EnumerationOptions options)
    {
        foreach (var directory in Directory.EnumerateDirectories(directoryPath, "*", options))
        {
            if (string.Equals(Path.GetFileName(directory), directoryName, StringComparison.OrdinalIgnoreCase))
                yield return directory;
        }
    }
}
