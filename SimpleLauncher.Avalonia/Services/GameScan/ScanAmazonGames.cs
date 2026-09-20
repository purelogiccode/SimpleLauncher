using Microsoft.Data.Sqlite;
using SimpleLauncher.Avalonia.Interfaces;
using SimpleLauncher.Core.Services.SanitizeInputString;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     Scans for installed Amazon Games and creates shortcuts for them.
/// </summary>
public class ScanAmazonGames : IGamePlatformScanner
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScanAmazonGames" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ScanAmazonGames(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task ScanAsync(GameScannerService gameScannerService, ILogger logErrors, string windowsRomsPath,
        string windowsImagesPath, ISet<string> ignoredGameNames)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Amazon Games", "Data", "Games", "Sql", "GameInstallInfo.sqlite");

            if (!File.Exists(dbPath)) return;

            // Use a connection string that opens in ReadOnly mode to avoid locking issues
            var connectionString = $"Data Source={dbPath};Mode=ReadOnly";

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, ProductTitle, InstallDirectory FROM DbSet WHERE Installed = 1";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                try
                {
                    if (!reader.IsDBNull(0))
                    {
                        var gameId = reader.GetString(0);
                        var title = reader.GetString(1);
                        var installDir = reader.GetString(2);

                        if (string.IsNullOrEmpty(title) || ignoredGameNames.Contains(title)) continue;
                        if (!Directory.Exists(installDir)) continue;

                        var sanitizedGameName = SanitizeInputSystemName.SanitizeFolderName(title);
                        var shortcutPath = Path.Combine(windowsRomsPath, $"{sanitizedGameName}.url");

                        // Amazon Games Protocol
                        var shortcutContent = $"[InternetShortcut]\nURL=amazon-games://play/{gameId}";
                        await File.WriteAllTextAsync(shortcutPath, shortcutContent);

                        await gameScannerService.FindAndSaveGameImageAsync(logErrors, title, installDir,
                            sanitizedGameName, windowsImagesPath);
                    }
                }
                catch (Exception ex)
                {
                    logErrors.Error(ex, "Error processing an Amazon game entry");
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't crash if SQLite is missing or DB is locked
            _logger.Debug($"[ScanAmazonGames] Error scanning Amazon games: {ex.Message}");
            logErrors.Error(ex, "Error scanning Amazon games");
        }
    }
}