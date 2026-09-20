using SimpleLauncher.Avalonia.Models;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Core.Services.CheckPaths;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Avalonia.Services.DisplaySystemInfo;

/// <summary>
///     Validates system configuration and produces human-readable system information.
///     Extracted from the WPF DisplaySystemInformation service — adapted for Avalonia
///     by returning data models instead of manipulating WPF UI elements directly.
/// </summary>
public class AvaloniaDisplaySystemInformation
{
    private readonly LocalizationService? _localization;

    public AvaloniaDisplaySystemInformation(LocalizationService? localization = null)
    {
        _localization = localization;
    }

    /// <summary>
    ///     Validates a system configuration (folders, image folder, emulator paths).
    /// </summary>
    public SystemValidationResult ValidateSystemConfiguration(SystemManagerConfig config)
    {
        var result = new SystemValidationResult();

        var allFoldersValid = config.SystemFolders.All(static folder =>
        {
            var resolvedSystemFolder = PathHelper.ResolveRelativeToAppDirectory(folder);
            return resolvedSystemFolder != null && CheckPath.IsValidPath(resolvedSystemFolder);
        });

        if (!allFoldersValid)
        {
            result.IsValid = false;
            result.AreSystemFoldersValid = false;
            // WPF parity: use localized strings with trailing newlines
            var systemFolderMsg = _localization?.GetString("SystemFolderpathisnotvalid") ??
                                  "System Folder path is not valid or does not exist:";
            result.ErrorMessages.Add($"{systemFolderMsg} '{string.Join(";", config.SystemFolders)}'\n\n");
        }

        if (!string.IsNullOrWhiteSpace(config.SystemImageFolder))
        {
            var resolvedSystemImageFolder = PathHelper.ResolveRelativeToAppDirectory(config.SystemImageFolder);
            if (resolvedSystemImageFolder == null || !CheckPath.IsValidPath(resolvedSystemImageFolder))
            {
                result.IsValid = false;
                result.IsSystemImageFolderValid = false;
                // WPF parity: use localized strings with trailing newlines
                var imageFolderMsg = _localization?.GetString("SystemImageFolderpathisnotvalid") ??
                                     "System Image Folder path is not valid or does not exist:";
                result.ErrorMessages.Add($"{imageFolderMsg} '{config.SystemImageFolder}'\n\n");
            }
        }

        foreach (var emulator in config.Emulators)
        {
            if (string.IsNullOrWhiteSpace(emulator.EmulatorLocation) ||
                CheckPath.IsValidEmulatorExecutablePath(emulator.EmulatorLocation))
            {
                continue;
            }

            result.IsValid = false;
            result.InvalidEmulatorLocations.Add(emulator.EmulatorLocation);
            // WPF parity: use localized strings with trailing newlines
            var emulatorMsg = _localization?.GetString("Emulatorpathisnotvalidfor") ?? "Emulator path is not valid for";
            result.ErrorMessages.Add($"{emulatorMsg} {emulator.EmulatorName}: '{emulator.EmulatorLocation}'\n\n");
        }

        return result;
    }

    /// <summary>
    ///     Builds the flat list of display lines shown when a system is selected,
    ///     mirroring the WPF <c>DisplaySystemInformation.DisplaySystemInfoAsync</c>
    ///     text block (hint line, configuration lines, one block per emulator).
    ///     Invalid folder/emulator paths are flagged so the UI renders them in red.
    /// </summary>
    public List<SystemInfoLine> BuildSystemInfoLines(SystemManagerConfig config)
    {
        var info = BuildSystemInfo(config);

        var lines = new List<SystemInfoLine>
        {
            new()
            {
                Text = Localized("Clickontheletterbuttonsabove",
                    "Click on the letter buttons above to see the games")
            },
            Blank(),
            new()
            {
                Text = $"{Localized("SystemFolder", "System Folder")}: {string.Join("; ", info.SystemFolders)}",
                IsError = !info.AreSystemFoldersValid
            },
            new()
            {
                Text =
                    $"{Localized("SystemImageFolder", "System Image Folder")}: {info.SystemImageFolder ?? Localized("DefaultImageFolder", "Using default image folder")}",
                IsError = !info.IsSystemImageFolderValid
            },
            new()
            {
                Text =
                    $"{Localized("ExtensiontoSearchintheSystemFolder2", "Extension to Search in the System Folder")}: {string.Join(", ", info.FileFormatsToSearch)}"
            },
            new()
            {
                Text =
                    $"{Localized("ExtractFileBeforeLaunch", "Extract File Before Launch?")}: {info.ExtractFileBeforeLaunch}"
            },
            new()
            {
                Text =
                    $"{Localized("ExtensiontoLaunchAfterExtraction2", "Extension to Launch After Extraction")}: {string.Join(", ", info.FileFormatsToLaunch)}"
            },
            new()
            {
                Text = $"{Localized("GroupFilesByFolder", "Group Files by Folder?")}: {info.GroupByFolder}"
            },
            new()
            {
                Text =
                    $"{Localized("DisableRecursiveSearch", "Disable recursive search")}: {info.DisableRecursiveSearch}"
            }
        };

        foreach (var emulator in info.Emulators)
        {
            lines.Add(Blank());
            lines.Add(new SystemInfoLine { Text = $"{Localized("EmulatorName", "Emulator Name")}: {emulator.Name}" });
            lines.Add(new SystemInfoLine
            {
                Text = $"{Localized("EmulatorPath", "Emulator Path")}: {emulator.Location}",
                IsError = !emulator.IsLocationValid
            });
            lines.Add(new SystemInfoLine
            {
                Text = $"{Localized("EmulatorParameters", "Emulator Parameters")}: {emulator.Parameters}"
            });
            lines.Add(new SystemInfoLine
            {
                Text =
                    $"{Localized("receiveNotificationEmulatorError", "Receive a Notification on Emulator Error?")}: {emulator.ReceiveErrorNotification}"
            });
        }

        return lines;

        // WPF used LineBreak between sections; a single space keeps the same line
        // height in Avalonia (an empty TextBlock would collapse to zero height).
        static SystemInfoLine Blank()
        {
            return new SystemInfoLine { Text = " " };
        }

        string Localized(string key, string fallback)
        {
            var value = _localization?.GetString(key);
            return string.IsNullOrEmpty(value) || string.Equals(value, key, StringComparison.OrdinalIgnoreCase)
                ? fallback
                : value;
        }
    }

    /// <summary>
    ///     Builds a structured system information model for display in any Avalonia UI.
    /// </summary>
    public SystemInfoModel BuildSystemInfo(SystemManagerConfig config)
    {
        var validation = ValidateSystemConfiguration(config);

        var emulators = config.Emulators.Select(e => new EmulatorInfoModel
        {
            Name = e.EmulatorName,
            Location = e.EmulatorLocation,
            Parameters = e.EmulatorParameters,
            ReceiveErrorNotification = e.ReceiveANotificationOnEmulatorError,
            IsLocationValid = !validation.InvalidEmulatorLocations.Contains(e.EmulatorLocation)
        }).ToList();

        return new SystemInfoModel
        {
            SystemName = config.SystemName,
            SystemFolders = config.SystemFolders.ToList(),
            SystemImageFolder = config.SystemImageFolder,
            FileFormatsToSearch = config.FileFormatsToSearch.ToList(),
            ExtractFileBeforeLaunch = config.ExtractFileBeforeLaunch,
            FileFormatsToLaunch = config.FileFormatsToLaunch.ToList(),
            GroupByFolder = config.GroupByFolder,
            DisableRecursiveSearch = config.DisableRecursiveSearch,
            Emulators = emulators,
            AreSystemFoldersValid = validation.AreSystemFoldersValid,
            IsSystemImageFolderValid = validation.IsSystemImageFolderValid,
            ValidationResult = validation
        };
    }
}