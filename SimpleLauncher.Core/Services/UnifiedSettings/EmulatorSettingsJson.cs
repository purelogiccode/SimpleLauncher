using System.Text.Json;

namespace SimpleLauncher.Core.Services.UnifiedSettings;

/// <summary>
///     Serializes individual emulator settings objects (e.g. <c>MameSettings</c>) to the
///     opaque JSON blobs stored in the EmulatorSettings table.
/// </summary>
public static class EmulatorSettingsJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>Serializes an emulator settings object to JSON.</summary>
    public static string Serialize<T>(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings, JsonOptions);
    }

    /// <summary>
    ///     Deserializes an emulator settings object. Returns null when the blob is corrupt
    ///     (callers keep the defaults and log).
    /// </summary>
    public static T? Deserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[UnifiedSettings] Skipping corrupt emulator settings for '{Type}'", typeof(T).Name);
            return null;
        }
    }
}