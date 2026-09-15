using System.Globalization;
using Microsoft.Data.Sqlite;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Provides functionality to inject Simple Launcher settings into the Stella emulator configuration database
///     (stella.sqlite3).
/// </summary>
public static class StellaConfigurationService
{
    /// <summary>
    ///     Injects Simple Launcher configuration settings into the Stella emulator's SQLite configuration database.
    ///     Creates the database from a sample if it does not exist, then upserts video, audio, and general settings.
    /// </summary>
    /// <param name="emulatorPath">The full path to the Stella emulator executable.</param>
    /// <param name="settings">The settings manager containing Stella configuration values.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public static void InjectSettings(string emulatorPath, SettingsManagerService settings,
        ILogger logger)
    {
        var emuDir = Path.GetDirectoryName(emulatorPath);
        if (string.IsNullOrEmpty(emuDir))
            throw new InvalidOperationException("Emulator directory not found.");

        var configPath = Path.Combine(emuDir, "stella.sqlite3");

        if (!File.Exists(configPath))
        {
            var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "Stella", "stella.sqlite3");
            if (File.Exists(samplePath))
            {
                try
                {
                    File.Copy(samplePath, configPath);
                    logger.Debug($"[StellaConfig] Created new stella.sqlite3 from sample: {configPath}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"[StellaConfig] Failed to create stella.sqlite3 from sample: {ex.Message}");
                    logger.Error(ex, $"[StellaConfig] Failed to create stella.sqlite3 from sample: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new FileNotFoundException("stella.sqlite3 not found and sample is missing.", samplePath);
            }
        }

        logger.Debug($"[StellaConfig] Injecting configuration into: {configPath}");

        var updates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "fullscreen", settings.Stella.Fullscreen ? "1" : "0" },
            { "vsync", settings.Stella.Vsync ? "true" : "false" },
            { "video", settings.Stella.VideoDriver },
            { "tia.correct_aspect", settings.Stella.CorrectAspect ? "true" : "false" },
            { "tv.filter", settings.Stella.TvFilter.ToString(CultureInfo.InvariantCulture) },
            { "tv.scanlines", settings.Stella.Scanlines.ToString(CultureInfo.InvariantCulture) },
            { "audio.enabled", settings.Stella.AudioEnabled ? "1" : "0" },
            { "audio.volume", settings.Stella.AudioVolume.ToString(CultureInfo.InvariantCulture) },
            { "dev.timemachine", settings.Stella.TimeMachine ? "1" : "0" },
            { "confirmexit", settings.Stella.ConfirmExit ? "1" : "0" }
        };

        var connectionString = new SqliteConnectionStringBuilder { DataSource = configPath }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        foreach (var kvp in updates)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO settings (setting, value) VALUES ($setting, $value) " +
                "ON CONFLICT(setting) DO UPDATE SET value = excluded.value;";
            command.Parameters.AddWithValue("$setting", kvp.Key);
            command.Parameters.AddWithValue("$value", kvp.Value);
            command.ExecuteNonQuery();
        }

        transaction.Commit();

        logger.Debug("[StellaConfig] Injection successful");
    }
}