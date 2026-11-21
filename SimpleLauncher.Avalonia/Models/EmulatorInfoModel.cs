namespace SimpleLauncher.Avalonia.Models;

/// <summary>
///     Emulator information for display.
/// </summary>
internal class EmulatorInfoModel
{
    public string Name { get; init; } = "";
    public string Location { get; init; } = "";
    public string Parameters { get; init; } = "";
    public bool ReceiveErrorNotification { get; init; }
    public bool IsLocationValid { get; init; }
}