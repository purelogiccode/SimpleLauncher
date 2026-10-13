namespace SimpleLauncher.Core.Models;

/// <summary>
///     Carries the input data needed to resolve or fix emulator launch parameters.
/// </summary>
public class ParameterResolverRequest
{
    /// <summary>
    ///     Gets or sets the name of the system the game belongs to.
    /// </summary>
    public string SystemName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the folder path of the system.
    /// </summary>
    public string SystemFolder { get; set; } = "";

    /// <summary>
    ///     Gets or sets the file formats searched for in the system folder.
    /// </summary>
    public IList<string> FileFormatsToSearch { get; set; } = [];

    /// <summary>
    ///     Gets or sets whether the game file must be extracted before launching.
    /// </summary>
    public bool ExtractFileBeforeLaunch { get; set; }

    /// <summary>
    ///     Gets or sets the file formats that are launched after extraction.
    /// </summary>
    public IList<string> FileFormatsToLaunch { get; set; } = [];

    /// <summary>
    ///     Gets or sets whether games are grouped by folder.
    /// </summary>
    public bool GroupByFolder { get; set; }

    /// <summary>
    ///     Gets or sets whether recursive folder search is disabled.
    /// </summary>
    public bool DisableRecursiveSearch { get; set; }

    /// <summary>
    ///     Gets or sets the name of the emulator used to launch the game.
    /// </summary>
    public string EmulatorName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the path to the emulator executable.
    /// </summary>
    public string EmulatorPath { get; set; } = "";

    /// <summary>
    ///     Gets or sets the current launch parameters to be fixed or resolved.
    /// </summary>
    public string CurrentParameters { get; set; } = "";

    /// <summary>
    ///     Gets or sets optional launch-failure context (emulator output and any local
    ///     diagnosis) so the AI can ground its suggestion in what actually failed —
    ///     e.g. MAME echoing a full ROM path as the system name (bugs 67522/67529).
    ///     Null when no failure context is available.
    /// </summary>
    public string? LaunchFailureContext { get; set; }
}