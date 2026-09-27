using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.MameManager;

namespace SimpleLauncher.Core.Services.MameData;

/// <summary>
///     Provides access to MAME machine data loaded from the mame.dat file, including a list of machines and a
///     name-to-description lookup dictionary.
/// </summary>
public class MameDataService : IMameDataService
{
    private readonly IMessageBoxLibraryService _messageBox;
    private bool _failureNotified;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MameDataService" /> class by loading MAME machine data from the data
    ///     file.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="messageBox">The message box service for displaying error dialogs if loading fails.</param>
    /// <param name="datPath">The full path to the mame.dat file; defaults to the application folder (test seam).</param>
    public MameDataService(ILogger logErrors, IMessageBoxLibraryService messageBox, string? datPath = null)
    {
        _messageBox = messageBox;

        // The constructor runs before the main window exists, so a message box has no owner yet and
        // would silently skip its dialog. Record the failure instead and let the startup
        // initialization report it once the window is shown (NotifyLoadFailureIfNeededAsync).
        var machines = MameManagerService.LoadFromDat(logErrors, datPath, messageBox,
            notifyUser: false, out var failure);
        LoadFailure = failure;

        Machines = machines.ToList();

        Lookup = machines
            .GroupBy(static m => m.MachineName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First().Description, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets the list of MAME machines loaded from the data file.
    /// </summary>
    public IReadOnlyList<MameManagerService> Machines { get; }

    /// <summary>
    ///     Gets a case-insensitive dictionary mapping machine names to their descriptions.
    /// </summary>
    public IDictionary<string, string> Lookup { get; }

    /// <summary>
    ///     Gets why loading mame.dat failed, or <see cref="MameDataLoadFailure.None" /> when it loaded.
    /// </summary>
    public MameDataLoadFailure LoadFailure { get; }

    /// <summary>
    ///     Reports a corrupt mame.dat to the user once a window exists to own the dialog. A corrupt file is
    ///     not covered by the required-files check (that check only verifies the file is present), so it
    ///     would otherwise fail silently; the missing-file case is left to that check to avoid two dialogs.
    ///     Safe to call more than once - the dialog is shown only on the first call.
    /// </summary>
    public async Task NotifyLoadFailureIfNeededAsync()
    {
        if (_failureNotified || LoadFailure != MameDataLoadFailure.CorruptedFile) return;
        _failureNotified = true;
        await _messageBox.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync();
    }
}
