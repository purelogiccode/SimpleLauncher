using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.CheckPaths;

namespace SimpleLauncher.Core.Services;

/// <summary>
///     Creates default system and image folders for a newly added system.
/// </summary>
public static class CreateDefaultSystemFoldersService
{
    /// <summary>
    ///     Creates the system folder, image folder, and all additional default folders for the specified system.
    /// </summary>
    /// <param name="systemName">The name of the system.</param>
    /// <param name="systemFolder">The path to the system's ROM folder.</param>
    /// <param name="systemImageFolder">The path to the system's image folder.</param>
    /// <param name="configuration">The application configuration for additional folder settings.</param>
    /// <param name="logErrors">The service used to log errors.</param>
    /// <param name="messageBox">The service used to display message boxes to the user.</param>
    public static async Task CreateFoldersAsync(string systemName, string systemFolder, string systemImageFolder,
        IConfiguration configuration, ILogger logErrors, IMessageBoxLibraryService messageBox)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var additionalFolders = configuration.GetValue<string[]>("AdditionalFolders") ??
        [
            "roms", "images", "title_snapshots", "gameplay_snapshots", "videos", "manuals", "walkthrough", "cabinets",
            "carts", "flyers", "pcbs"
        ];
        var resolvedSystemFolder = PathHelper.ResolveRelativeToAppDirectory(systemFolder);
        var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(systemImageFolder);

        try
        {
            if (!string.IsNullOrEmpty(resolvedSystemFolder) && !Directory.Exists(resolvedSystemFolder))
            {
                try
                {
                    Directory.CreateDirectory(resolvedSystemFolder);
                }
                catch (Exception ex)
                {
                    // Notify developer
                    logErrors.Error(ex, "Error creating the primary system folder");

                    // Notify user
                    await messageBox.FolderCreationFailedMessageBoxAsync();
                }
            }

            if (!string.IsNullOrEmpty(resolvedSystemImageFolder) && !Directory.Exists(resolvedSystemImageFolder))
            {
                try
                {
                    Directory.CreateDirectory(resolvedSystemImageFolder);
                }
                catch (Exception ex)
                {
                    // Notify developer
                    logErrors.Error(ex, "Error creating the primary image folder");

                    // Notify user
                    await messageBox.FolderCreationFailedMessageBoxAsync();
                }
            }

            foreach (var folder in additionalFolders)
            {
                var folderPath = Path.Combine(baseDirectory, folder, systemName);
                if (Directory.Exists(folderPath)) continue;

                try
                {
                    Directory.CreateDirectory(folderPath);
                }
                catch (Exception ex)
                {
                    // Notify developer
                    logErrors.Error(ex, $"Error creating the {folder} folder.");

                    // Notify user
                    await messageBox.FolderCreationFailedMessageBoxAsync();
                }
            }
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage =
                "The application failed to create the necessary folders for the newly added system.";
            logErrors.Error(ex, contextMessage);

            // Notify user
            await messageBox.FolderCreationFailedMessageBoxAsync();

            throw;
        }
    }
}