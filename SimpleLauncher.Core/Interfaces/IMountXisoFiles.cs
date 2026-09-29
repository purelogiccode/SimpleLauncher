using SimpleLauncher.Core.Services.GameLauncher.MountFiles;

namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Mounts original Xbox ISO (XISO) images using SimpleXisoDrive.exe and the Dokan filesystem driver.
/// </summary>
public interface IMountXisoFiles
{
    /// <summary>
    ///     Mounts an XISO file and returns a disposable drive handle with the mounted default.xbe path.
    /// </summary>
    /// <param name="resolvedIsoFilePath">The full path to the XISO file to mount.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="messageBox">The message box service for user notifications.</param>
    /// <returns>
    ///     A task representing the asynchronous operation, resulting in a <see cref="MountXisoDrive" /> with the mounted
    ///     default.xbe path.
    /// </returns>
    Task<MountXisoDrive> MountAsync(string resolvedIsoFilePath, ILogger logErrors,
        IMessageBoxLibraryService messageBox);

    /// <summary>
    ///     Mounts a compressed Xbox image (.cso/.zar) with SimpleXisoDrive's virtual image.iso option
    ///     and returns a disposable drive handle with the mounted image.iso path.
    /// </summary>
    /// <param name="resolvedFilePath">The full path to the .cso/.zar file to mount.</param>
    /// <param name="logErrors">The error logger.</param>
    /// <param name="messageBox">The message box service for user notifications.</param>
    /// <returns>
    ///     A task representing the asynchronous operation, resulting in a <see cref="MountXisoDrive" /> with the mounted
    ///     image.iso path.
    /// </returns>
    Task<MountXisoDrive> MountImageIsoAsync(string resolvedFilePath, ILogger logErrors,
        IMessageBoxLibraryService messageBox);
}