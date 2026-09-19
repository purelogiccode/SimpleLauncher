namespace SimpleLauncher.Avalonia.Interfaces;

using Models;

/// <summary>
///     UI surface the system selection orchestrator drives: the top System/Emulator
///     combo boxes, the sidebar, and the ROM folder watcher.
/// </summary>
public interface ISystemSelectionHost
{
    /// <summary>Gets or sets the play-time string shown for the selected system.</summary>
    string PlayTime { get; set; }

    /// <summary>Gets or sets whether the play-time display is visible for the selected system.</summary>
    bool IsPlayTimeVisible { get; set; }

    /// <summary>Gets or sets the current MAME sort order ("FileName" or "MachineDescription").</summary>
    string MameSortOrder { get; set; }

    /// <summary>Sets the items of the top System ComboBox (sorted system names).</summary>
    void SetSystemComboBoxItems(IReadOnlyList<string> systemNames);

    /// <summary>Gets the currently selected system name from the top System ComboBox (null when none).</summary>
    string? GetSelectedSystem();

    /// <summary>
    ///     Sets the items of the Emulator ComboBox for the selected system, selecting the
    ///     first emulator when one exists (WPF EmulatorComboBox parity).
    /// </summary>
    void SetEmulatorComboBoxItems(IReadOnlyList<string> emulatorNames);

    /// <summary>Navigates the game browser to the given system (empty string = All Games).</summary>
    void NavigateToSystem(string systemName);

    /// <summary>
    ///     Displays the selected system's configuration summary in the game browser
    ///     (WPF DisplaySystemInformation parity). Hidden again as soon as games load.
    /// </summary>
    void ShowSystemInformation(IReadOnlyList<SystemInfoLine> lines);

    /// <summary>Rebuilds the sidebar from system.xml and refreshes its count badges.</summary>
    void RefreshSidebar();

    /// <summary>Restarts the ROM folder watcher over all configured systems.</summary>
    void RestartFileWatcher();
}