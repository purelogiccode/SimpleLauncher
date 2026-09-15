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
        var baseDirectory = AppContext.BaseDirectory;
        var requiredFiles = configuration.GetValue<string[]>("RequiredFiles") ??
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
        try
        {
            var missingFiles = requiredFiles
                .Select(f => Path.Combine(baseDirectory, f))
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
}