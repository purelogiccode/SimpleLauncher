using System.Text.Json;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.UnifiedSettings;

/// <summary>
///     JSON persistence model for <see cref="SystemManagerConfig" /> inside the unified database.
///     A dedicated DTO (instead of serializing the model directly) keeps the on-disk format
///     stable when the model gains init-only or required members.
/// </summary>
public sealed class SystemConfigData
{
    /// <summary>Display name of the system.</summary>
    public string SystemName { get; set; } = "";

    /// <summary>Folders scanned for the system's games.</summary>
    public List<string> SystemFolders { get; set; } = [];

    /// <summary>Folder containing the system's cover images.</summary>
    public string SystemImageFolder { get; set; } = "";

    /// <summary>File extensions included when scanning the system folders.</summary>
    public List<string> FileFormatsToSearch { get; set; } = [];

    /// <summary>File extensions that may be launched for the system.</summary>
    public List<string> FileFormatsToLaunch { get; set; } = [];

    /// <summary>Whether archives are extracted before launch.</summary>
    public bool ExtractFileBeforeLaunch { get; set; }

    /// <summary>Whether the game list is grouped by sub-folder.</summary>
    public bool GroupByFolder { get; set; }

    /// <summary>Whether sub-folders are excluded from the recursive scan.</summary>
    public bool DisableRecursiveSearch { get; set; }

    /// <summary>Emulators configured for the system.</summary>
    public List<EmulatorData> Emulators { get; set; } = [];
}

/// <summary>JSON persistence model for <see cref="Emulator" />.</summary>
public sealed class EmulatorData
{
    /// <summary>Display name of the emulator.</summary>
    public string EmulatorName { get; set; } = "";

    /// <summary>Path to the emulator executable.</summary>
    public string EmulatorLocation { get; set; } = "";

    /// <summary>Command-line parameters used to launch a game.</summary>
    public string EmulatorParameters { get; set; } = "";

    /// <summary>Whether launch errors are reported in a message box.</summary>
    public bool ReceiveANotificationOnEmulatorError { get; set; } = true;

    /// <summary>Primary image-pack download URL.</summary>
    public string ImagePackDownloadLink { get; set; } = "";

    /// <summary>Secondary image-pack download URL.</summary>
    public string ImagePackDownloadLink2 { get; set; } = "";

    /// <summary>Third image-pack download URL.</summary>
    public string ImagePackDownloadLink3 { get; set; } = "";

    /// <summary>Fourth image-pack download URL.</summary>
    public string ImagePackDownloadLink4 { get; set; } = "";

    /// <summary>Fifth image-pack download URL.</summary>
    public string ImagePackDownloadLink5 { get; set; } = "";

    /// <summary>Folder where downloaded image packs are extracted.</summary>
    public string ImagePackDownloadExtractPath { get; set; } = "";
}

/// <summary>Serializes <see cref="SystemManagerConfig" /> instances to and from the JSON blobs in the Systems table.</summary>
public static class SystemConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>Serializes a system config to its database JSON representation.</summary>
    public static string Serialize(SystemManagerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var data = new SystemConfigData
        {
            SystemName = config.SystemName ?? "",
            SystemFolders = config.SystemFolders?.ToList() ?? [],
            SystemImageFolder = config.SystemImageFolder ?? "",
            FileFormatsToSearch = config.FileFormatsToSearch?.ToList() ?? [],
            FileFormatsToLaunch = config.FileFormatsToLaunch?.ToList() ?? [],
            ExtractFileBeforeLaunch = config.ExtractFileBeforeLaunch,
            GroupByFolder = config.GroupByFolder,
            DisableRecursiveSearch = config.DisableRecursiveSearch,
            Emulators = config.Emulators?.Select(static e => new EmulatorData
            {
                EmulatorName = e.EmulatorName ?? "",
                EmulatorLocation = e.EmulatorLocation ?? "",
                EmulatorParameters = e.EmulatorParameters ?? "",
                ReceiveANotificationOnEmulatorError = e.ReceiveANotificationOnEmulatorError,
                ImagePackDownloadLink = e.ImagePackDownloadLink ?? "",
                ImagePackDownloadLink2 = e.ImagePackDownloadLink2 ?? "",
                ImagePackDownloadLink3 = e.ImagePackDownloadLink3 ?? "",
                ImagePackDownloadLink4 = e.ImagePackDownloadLink4 ?? "",
                ImagePackDownloadLink5 = e.ImagePackDownloadLink5 ?? "",
                ImagePackDownloadExtractPath = e.ImagePackDownloadExtractPath ?? ""
            }).ToList() ?? []
        };
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    /// <summary>
    ///     Deserializes a system config. Returns null when the blob is corrupt (callers skip the entry and log).
    /// </summary>
    public static SystemManagerConfig? Deserialize(string systemName, string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SystemConfigData>(json, JsonOptions);
            if (data is null)
                return null;

            var name = string.IsNullOrWhiteSpace(data.SystemName) ? systemName : data.SystemName;
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return new SystemManagerConfig
            {
                SystemName = name,
                SystemFolders = data.SystemFolders ?? [],
                SystemImageFolder = data.SystemImageFolder ?? "",
                FileFormatsToSearch = data.FileFormatsToSearch ?? [],
                FileFormatsToLaunch = data.FileFormatsToLaunch ?? [],
                ExtractFileBeforeLaunch = data.ExtractFileBeforeLaunch,
                GroupByFolder = data.GroupByFolder,
                DisableRecursiveSearch = data.DisableRecursiveSearch,
                Emulators = data.Emulators?.Select(static e => new Emulator
                {
                    EmulatorName = e.EmulatorName ?? "",
                    EmulatorLocation = e.EmulatorLocation ?? "",
                    EmulatorParameters = e.EmulatorParameters ?? "",
                    ReceiveANotificationOnEmulatorError = e.ReceiveANotificationOnEmulatorError,
                    ImagePackDownloadLink = e.ImagePackDownloadLink ?? "",
                    ImagePackDownloadLink2 = e.ImagePackDownloadLink2 ?? "",
                    ImagePackDownloadLink3 = e.ImagePackDownloadLink3 ?? "",
                    ImagePackDownloadLink4 = e.ImagePackDownloadLink4 ?? "",
                    ImagePackDownloadLink5 = e.ImagePackDownloadLink5 ?? "",
                    ImagePackDownloadExtractPath = e.ImagePackDownloadExtractPath ?? ""
                }).ToList() ?? []
            };
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[UnifiedSettings] Skipping corrupt system entry '{System}'", systemName);
            return null;
        }
    }
}