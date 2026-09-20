namespace SimpleLauncher.Avalonia.Models;

/// <summary>
///     One display line of the selected system's configuration summary
///     (WPF DisplaySystemInformation TextBlock/Run parity). <see cref="IsError" />
///     marks lines whose folder/emulator path failed validation so the UI can
///     render them in the error brush.
/// </summary>
public sealed class SystemInfoLine
{
    public string Text { get; init; } = "";
    public bool IsError { get; init; }
}