using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.CleanAndDeleteFiles;

namespace SimpleLauncher.Core.Services.CheckIfDirectoryIsWritable;

/// <summary>
///     Validates whether a directory is writable by creating and deleting a temporary test file.
/// </summary>
public class DirectoryValidationService : IDirectoryValidationService
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DirectoryValidationService" /> class.
    /// </summary>
    /// <param name="logErrors">The logger used to record validation failures.</param>
    public DirectoryValidationService(ILogger logErrors)
    {
        _logger = logErrors;
    }

    /// <summary>
    ///     Determines whether the given directory is writable by creating and deleting a temporary test file.
    /// </summary>
    /// <param name="path">The path of the directory to test.</param>
    /// <returns>True if the directory is writable, false otherwise.</returns>
    public bool IsWritableDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return false;

            var testFile = Path.Combine(path, Guid.NewGuid() + ".tmp");

            using (var fs = new FileStream(testFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                fs.Close();
            }

            DeleteFiles.TryDeleteFile(testFile);

            return true;
        }
        catch (Exception ex)
        {
            // Expected condition: directory is not writable (e.g. Program Files).
            // Log at Information level so the bug report API does not pick it up.
            _logger.Information(ex, "Directory is not writable: {Path}", path);

            return false;
        }
    }
}