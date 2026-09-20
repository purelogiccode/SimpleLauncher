using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services;

/// <summary>
///     Checks that all files required by the application are present at startup.
/// </summary>
public class CheckForRequiredFilesService
{
    private readonly IMessageBoxLibraryService _messageBoxLibrary;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CheckForRequiredFilesService" /> class.
    /// </summary>
    /// <param name="messageBoxLibrary">The message box service used to report missing files.</param>
    public CheckForRequiredFilesService(IMessageBoxLibraryService messageBoxLibrary)
    {
        _messageBoxLibrary = messageBoxLibrary;
    }

    /// <summary>
    ///     Verifies that all configured required files exist in the application directory and notifies the user of any missing
    ///     files.
    /// </summary>
    /// <param name="configuration">The application configuration containing the required files list.</param>
    /// <param name="logErrors">The logger used to record failures.</param>
    /// <returns>A task representing the asynchronous check operation.</returns>
    public async Task CheckFilesAsync(IConfiguration configuration, ILogger logErrors)
    {
        await CheckFilesAsync(configuration, logErrors, AppContext.BaseDirectory);
    }

    /// <summary>
    ///     Test seam: verifies the configured required files against an explicit base directory.
    /// </summary>
    /// <param name="configuration">The application configuration containing the required files list.</param>
    /// <param name="logErrors">The logger used to record failures.</param>
    /// <param name="baseDirectory">The directory the required files are resolved against.</param>
    /// <returns>A task representing the asynchronous check operation.</returns>
    internal async Task CheckFilesAsync(IConfiguration configuration, ILogger logErrors, string baseDirectory)
    {
        var requiredFiles = configuration.GetSection("RequiredFiles").Get<string[]>();
        if (requiredFiles is null || requiredFiles.Length == 0)
        {
            requiredFiles =
            [
                "images\\default.png",
                @"images\systems\default.png",
                "audio\\click.mp3",
                "audio\\notification.mp3",
                "audio\\shutter.mp3",
                "audio\\trash.mp3",
                "appsettings.json",
                "mame.dat"
            ];
        }
        try
        {
            // The configuration uses Windows separators; normalize them so the check works
            // on Linux/macOS too (Path.Combine would otherwise treat "images\default.png"
            // as a single file name containing a backslash).
            var missingFiles = requiredFiles
                .Select(f => Path.Combine(baseDirectory, NormalizeSeparators(f)))
                .Where(static f => !File.Exists(f))
                .ToList();

            if (missingFiles.Count == 0) return;

            var fileList = string.Join(Environment.NewLine, missingFiles);
            await _messageBoxLibrary.HandleMissingRequiredFilesMessageBoxAsync(fileList);
        }
        catch (Exception ex)
        {
            logErrors.Error(ex, "Failed to check for required files");
        }
    }

    /// <summary>
    ///     Converts the Windows-style separators used in the configuration to the current
    ///     platform's directory separator.
    /// </summary>
    private static string NormalizeSeparators(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }
}