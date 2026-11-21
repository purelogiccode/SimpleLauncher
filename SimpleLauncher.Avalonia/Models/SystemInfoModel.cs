using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Models;

/// <summary>
///     Structured system information for display in Avalonia UI.
/// </summary>
internal class SystemInfoModel
{
    public string SystemName { get; init; } = "";
    public List<string> SystemFolders { get; init; } = [];
    public string? SystemImageFolder { get; init; }
    public List<string> FileFormatsToSearch { get; init; } = [];
    public bool ExtractFileBeforeLaunch { get; init; }
    public List<string> FileFormatsToLaunch { get; init; } = [];
    public bool GroupByFolder { get; init; }
    public bool DisableRecursiveSearch { get; init; }
    public List<EmulatorInfoModel> Emulators { get; init; } = [];
    public bool AreSystemFoldersValid { get; init; }
    public bool IsSystemImageFolderValid { get; init; }
    public SystemValidationResult ValidationResult { get; init; } = new();
}