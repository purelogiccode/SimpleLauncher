using SimpleLauncher.Core.Services.MameData;
using SimpleLauncher.Core.Services.MameManager;

namespace SimpleLauncher.Core.Interfaces;

/// <summary>
///     Provides access to MAME machine data loaded from the MAME data source.
/// </summary>
public interface IMameDataService
{
    /// <summary>
    ///     Gets the list of MAME machines available in the data source.
    /// </summary>
    IReadOnlyList<MameManagerService> Machines { get; }

    /// <summary>
    ///     Gets the lookup dictionary mapping machine names to their descriptions.
    /// </summary>
    IDictionary<string, string> Lookup { get; }

    /// <summary>
    ///     Gets why loading mame.dat failed, or <see cref="MameDataLoadFailure.None" /> when it loaded.
    /// </summary>
    MameDataLoadFailure LoadFailure { get; }

    /// <summary>
    ///     Reports a load failure that could not be shown during startup (no window owner yet).
    ///     Safe to call more than once - the dialog is shown only on the first call.
    /// </summary>
    Task NotifyLoadFailureIfNeededAsync();
}
