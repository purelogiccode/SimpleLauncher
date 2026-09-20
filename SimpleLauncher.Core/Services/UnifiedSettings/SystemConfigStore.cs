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
    public string SystemName { get; set; } = "";
    public List<string> SystemFolders { get; set; } = [];
    public string SystemImageFolder { get; set; } = "";
    public List<string> FileFormatsToSearch { get; set; } = [];
    public List<string> FileFormatsToLaunch { get; set; } = [];
    public bool ExtractFileBeforeLaunch { get; set; }
    public bool GroupByFolder { get; set; }
    public bool DisableRecursiveSearch { get; set; }
    public List<EmulatorData> Emulators { get; set; } = [];
}

/// <summary>JSON persistence model for <see cref="Emulator" />.</summary>
public sealed class EmulatorData
{
    public string EmulatorName { get; set; } = "";
    public string EmulatorLocation { get; set; } = "";
    public string EmulatorParameters { get; set; } = "";
    public bool ReceiveANotificationOnEmulatorError { get; set; } = true;
    public string ImagePackDownloadLink { get; set; } = "";
    public string ImagePackDownloadLink2 { get; set; } = "";
    public string ImagePackDownloadLink3 { get; set; } = "";
    public string ImagePackDownloadLink4 { get; set; } = "";
    public string ImagePackDownloadLink5 { get; set; } = "";
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