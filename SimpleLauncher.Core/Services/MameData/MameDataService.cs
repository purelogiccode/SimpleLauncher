using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.MameManager;

namespace SimpleLauncher.Core.Services.MameData;

/// <summary>
///     Provides access to MAME machine data loaded from the mame.dat file, including a list of machines and a
///     name-to-description lookup dictionary.
/// </summary>
public class MameDataService : IMameDataService
{
    private readonly Lazy<MameDataSnapshot> _data;
    private readonly ILogger _logErrors;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly Task _warmupTask;
    private bool _failureNotified;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MameDataService" /> class and starts loading MAME machine
    ///     data from the data file in the background.
    /// </summary>
    /// <param name="logErrors">The logger instance for error logging.</param>
    /// <param name="messageBox">The message box service for displaying error dialogs if loading fails.</param>
    /// <param name="datPath">The full path to the mame.dat file; defaults to the application folder (test seam).</param>
    public MameDataService(ILogger logErrors, IMessageBoxLibraryService messageBox, string? datPath = null)
    {
        _logErrors = logErrors;
        _messageBox = messageBox;

        // mame.dat is a multi-megabyte binary file that used to be parsed synchronously
        // while the DI container resolved the main window, adding ~200 ms to startup.
        // The data is not needed to show the window, so load it on the thread pool and
        // let the first game scan (or the failure check) await the same lazy value.
        _data = new Lazy<MameDataSnapshot>(() => Load(datPath), LazyThreadSafetyMode.ExecutionAndPublication);
        _warmupTask = Task.Run(() =>
        {
            try
            {
                _ = _data.Value;
            }
            catch (Exception ex)
            {
                logErrors.Error(ex, "Failed to load mame.dat in the background");
            }
        });
    }

    /// <summary>
    ///     Gets the list of MAME machines loaded from the data file.
    /// </summary>
    public IReadOnlyList<MameManagerService> Machines => _data.Value.Machines;

    /// <summary>
    ///     Gets a case-insensitive dictionary mapping machine names to their descriptions.
    /// </summary>
    public IDictionary<string, string> Lookup => _data.Value.Lookup;

    /// <summary>
    ///     Gets why loading mame.dat failed, or <see cref="MameDataLoadFailure.None" /> when it loaded.
    /// </summary>
    public MameDataLoadFailure LoadFailure => _data.Value.Failure;

    private MameDataSnapshot Load(string? datPath)
    {
        // The constructor runs before the main window exists, so a message box has no owner yet and
        // would silently skip its dialog. Record the failure instead and let the startup
        // initialization report it once the window is shown (NotifyLoadFailureIfNeededAsync).
        var machines = MameManagerService.LoadFromDat(_logErrors, datPath, _messageBox,
            notifyUser: false, out var failure);

        var machineList = machines.ToList();
        var lookup = machineList
            .GroupBy(static m => m.MachineName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First().Description, StringComparer.OrdinalIgnoreCase);

        return new MameDataSnapshot(machineList, lookup, failure);
    }

    /// <summary>
    ///     Reports a corrupt mame.dat to the user once a window exists to own the dialog. A corrupt file is
    ///     not covered by the required-files check (that check only verifies the file is present), so it
    ///     would otherwise fail silently; the missing-file case is left to that check to avoid two dialogs.
    ///     Safe to call more than once - the dialog is shown only on the first call.
    /// </summary>
    public async Task NotifyLoadFailureIfNeededAsync()
    {
        // Make sure the background load has finished before reading the failure state.
        await _warmupTask;
        if (_failureNotified || LoadFailure != MameDataLoadFailure.CorruptedFile) return;
        _failureNotified = true;
        await _messageBox.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync();
    }

    private sealed record MameDataSnapshot(IReadOnlyList<MameManagerService> Machines,
        IDictionary<string, string> Lookup, MameDataLoadFailure Failure);
}
