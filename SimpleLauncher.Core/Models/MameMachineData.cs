using MessagePack;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Models;

/// <summary>
///     Represents a MAME machine entry with its machine name and description.
/// </summary>
[MessagePackObject]
public class MameMachineData
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
    ///     Loads the MAME machine list from the mame.dat file.
    /// </summary>
    /// <param name="logErrors">The logger used to record warnings and errors.</param>
    /// <param name="datPath">An optional path to the mame.dat file; defaults to the application folder.</param>
    /// <param name="messageBox">An optional message box service used to notify the user when the file is missing or corrupted.</param>
    /// <returns>The list of MAME machines, or an empty list if the file could not be loaded.</returns>
    public static IList<MameMachineData> LoadFromDat(ILogger logErrors, string? datPath = null,
        IMessageBoxLibraryService? messageBox = null)
    {
        datPath ??= DefaultDatPath;

        if (!File.Exists(datPath))
        {
            // A missing mame.dat is an expected user condition (fresh install or portable
            // copy without the data file — bug 67737): Information level so it is never
            // reported as a bug.
            const string contextMessage = "The file 'mame.dat' could not be found in the application folder.";
            logErrors.Information(contextMessage);

            // Notify user
            if (messageBox != null) _ = messageBox.ReinstallSimpleLauncherFileMissingMessageBoxAsync();

            return []; // return an empty list
        }

        try
        {
            // Read the binary data from the DAT file
            var binaryData = File.ReadAllBytes(datPath);

            // Deserialize the binary data to a list of MameMachineData objects
            return MessagePackSerializer.Deserialize<List<MameMachineData>>(binaryData);
        }
        catch (Exception ex)
        {
            // A foreign, truncated, or version-skewed mame.dat is the user's data failing,
            // not app code (bug 67738 — e.g. a text file dropped in as mame.dat): an expected
            // user condition, logged at Information so it is never reported as a bug. The user
            // is still notified via the reinstall dialog below.
            const string contextMessage = "The file mame.dat could not be loaded or is corrupted.";
            logErrors.Information(ex, contextMessage);

            // Notify user
            if (messageBox != null) _ = messageBox.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync();

            return []; // return an empty list
        }
    }
}