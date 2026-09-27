using MessagePack;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.MameData;

namespace SimpleLauncher.Core.Services.MameManager;

/// <summary>
///     Represents a MAME machine entry loaded from the mame.dat file.
/// </summary>
[MessagePackObject]
public class MameManagerService
{
    private static readonly string DefaultDatPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mame.dat");

    /// <summary>
    ///     Gets or sets the machine name of the MAME entry.
    /// </summary>
    [Key(0)]
    public string MachineName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the human-readable description of the MAME machine.
    /// </summary>
    [Key(1)]
    public string Description { get; set; } = "";

    /// <summary>
    ///     Loads the list of MAME machines from the mame.dat binary file.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="datPath">The full path to the mame.dat file; defaults to the application folder.</param>
    /// <param name="messageBox">Optional message box service used to notify the user when the file is missing or corrupted.</param>
    /// <returns>A list of <see cref="MameManagerService" /> entries, or an empty list if the file cannot be loaded.</returns>
    public static IList<MameManagerService> LoadFromDat(ILogger logErrors, string? datPath = null,
        IMessageBoxLibraryService? messageBox = null)
    {
        return LoadFromDat(logErrors, datPath, messageBox, notifyUser: true, out _);
    }

    /// <summary>
    ///     Loads the list of MAME machines from the mame.dat binary file and reports why loading failed.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="datPath">The full path to the mame.dat file; defaults to the application folder.</param>
    /// <param name="messageBox">Optional message box service used to notify the user when the file is missing or corrupted.</param>
    /// <param name="notifyUser">
    ///     When <c>false</c>, the caller owns the user notification (it may run before a window exists to own
    ///     the dialog) and should report the returned <paramref name="failure" /> later.
    /// </param>
    /// <param name="failure">Receives why the load failed, or <see cref="MameDataLoadFailure.None" /> on success.</param>
    /// <returns>A list of <see cref="MameManagerService" /> entries, or an empty list if the file cannot be loaded.</returns>
    internal static IList<MameManagerService> LoadFromDat(ILogger logErrors, string? datPath,
        IMessageBoxLibraryService? messageBox, bool notifyUser, out MameDataLoadFailure failure)
    {
        failure = MameDataLoadFailure.None;
        datPath ??= DefaultDatPath;

        if (!File.Exists(datPath))
        {
            failure = MameDataLoadFailure.MissingFile;

            // Notify developer
            const string contextMessage = "The file 'mame.dat' could not be found in the application folder.";
            logErrors.Warning(contextMessage);

            // Notify user
            if (notifyUser && messageBox != null) _ = messageBox.ReinstallSimpleLauncherFileMissingMessageBoxAsync();

            return []; // return an empty list
        }

        try
        {
            // Read the binary data from the DAT file
            var binaryData = File.ReadAllBytes(datPath);

            // Deserialize the binary data to a list of MameManagerService objects
            return MessagePackSerializer.Deserialize<List<MameManagerService>>(binaryData);
        }
        catch (Exception ex)
        {
            failure = MameDataLoadFailure.CorruptedFile;

            // Notify developer
            const string contextMessage = "The file mame.dat could not be loaded or is corrupted.";
            logErrors.Error(ex, contextMessage);

            // Notify user
            if (notifyUser && messageBox != null) _ = messageBox.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync();

            return []; // return an empty list
        }
    }
}