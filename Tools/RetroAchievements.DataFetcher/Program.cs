using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using MessagePack;
using RetroAchievements.DataFetcher.Models;
using RetroAchievements.DataFetcher.Services.DebugAndBugReport;
using Serilog.Events;

namespace RetroAchievements.DataFetcher;

file static class Program
{
    // Paths are anchored to the executable folder: relative paths used to resolve against the
    // process CWD, so shortcuts with a different "Start in", dotnet run or scheduled tasks
    // read/wrote the wrong files.
    private static readonly string SettingsFilePath = Path.Combine(AppContext.BaseDirectory, "settings.xml");
    private static readonly string ConsoleListFilePath = Path.Combine(AppContext.BaseDirectory, "consoles.txt");
    private static readonly string OutputFileNameJson = Path.Combine(AppContext.BaseDirectory, "RetroAchievements.json");
    private static readonly string OutputFileNameMsgPack = Path.Combine(AppContext.BaseDirectory, "RetroAchievements.dat");
    private const string BaseApiUrl = "https://retroachievements.org/API";

    private static async Task Main(string[] args)
    {
        var appDataLogFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(appDataLogFolder);

        var bugReportSink = new BugReportApiSink(appDataLogFolder);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Level:u3}] {Timestamp:HH:mm:ss} {Message}{NewLine}{Exception}")
            .WriteTo.Async(a => a.File(
                Path.Combine(appDataLogFolder, "error_user.log"),
                LogEventLevel.Warning,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7))
            .WriteTo.Sink(bugReportSink)
            .CreateLogger();

        try
        {
            if (args.Length == 1 && Path.GetExtension(args[0]).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                await RunConversionModeAsync(args[0]);
                return;
            }

            await RunFetchModeAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled error occurred");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task RunConversionModeAsync(string jsonFilePath)
    {
        Log.Information("Conversion mode: Processing JSON file '{FilePath}'...", jsonFilePath);

        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
            var games = JsonSerializer.Deserialize<List<GameInfo>>(jsonContent);

            if (games == null || games.Count == 0)
            {
                Log.Error("The JSON file is empty or invalid. No data to convert");
            }
            else
            {
                var msgPackFilePath = Path.ChangeExtension(jsonFilePath, ".dat");
                Log.Information("Converting {GameCount:N0} games to MessagePack...", games.Count);
                var msgPackData = MessagePackSerializer.Serialize(games);
                await File.WriteAllBytesAsync(msgPackFilePath, msgPackData);
                Log.Information("MessagePack file saved as '{FilePath}'", msgPackFilePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Conversion failed");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static async Task RunFetchModeAsync()
    {
        var settings = await LoadOrPromptSettings();
        using HttpClient client = new();

        Log.Information("Starting RetroAchievements game data fetcher...");
        Log.Information("Authenticated as: {Username}", settings.Username);

        var allGames = new List<GameInfo>();
        var serializerOptions = new JsonSerializerOptions { WriteIndented = true };

        try
        {
            // Fetch consoles
            Log.Information("Fetching console list...");
            var consoles = await FetchConsoles(client, settings);

            if (consoles.Count == 0)
            {
                Log.Error("No consoles found. Aborting");
                // Flush before the process exits: Environment.Exit bypasses Main's finally,
                // which is exactly when the buffered logs are needed most.
                Log.CloseAndFlush();
                Environment.Exit(1);
            }

            Log.Information("Found {ConsoleCount:N0} consoles", consoles.Count);
            await SaveConsoleListAsync(consoles);
            Console.WriteLine();

            // Filter for active game systems only
            var activeConsoles = consoles.Where(static c => c is { Active: true, IsGameSystem: true }).ToList();
            Log.Information("Processing {ActiveCount} active game consoles...", activeConsoles.Count);

            // Fetch games for each console
            var totalGames = await FetchGamesForAllConsolesAsync(client, settings, activeConsoles, allGames);

            Console.WriteLine();
            Log.Information("Total games fetched: {TotalGames:N0}", totalGames);

            // Save results
            if (allGames.Count > 0)
                await SaveGameDataAsync(allGames, serializerOptions);
            else
                Log.Warning("No games were found to save");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Critical error during fetch");
            Log.Error("Process incomplete");
            Log.CloseAndFlush();
            Environment.Exit(1);
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static async Task<List<ConsoleInfo>> FetchConsoles(HttpClient client, RaSettings settings)
    {
        try
        {
            var auth = $"u={settings.Username}&y={settings.WebApiKey}";
            var url = $"{BaseApiUrl}/API_GetConsoleIDs.php?{auth}";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var consoles = JsonSerializer.Deserialize<List<ConsoleInfo>>(json);

            return consoles ?? new List<ConsoleInfo>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch consoles from RetroAchievements API");
            throw;
        }
    }

    private static async Task<int> FetchGamesForAllConsolesAsync(
        HttpClient client,
        RaSettings settings,
        List<ConsoleInfo> consoles,
        List<GameInfo> allGames)
    {
        var totalGames = 0;
        var auth = $"u={settings.Username}&y={settings.WebApiKey}";

        for (var i = 0; i < consoles.Count; i++)
        {
            var console = consoles[i];
            Log.Information("[{Index}/{Total}] Fetching games for '{ConsoleName}' (ID: {ConsoleId})...", i + 1,
                consoles.Count, console.Name, console.Id);

            try
            {
                var url = $"{BaseApiUrl}/API_GetGameList.php?{auth}&i={console.Id}&h=1&f=1";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Failed to fetch games for console {ConsoleName}: HTTP {StatusCode}", console.Name,
                        response.StatusCode);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                var games = JsonSerializer.Deserialize<List<GameInfo>>(json);

                if (games?.Count > 0)
                {
                    allGames.AddRange(games);
                    totalGames += games.Count;
                    Log.Information("Found {GameCount:N0} games for {ConsoleName}", games.Count, console.Name);
                }
                else
                {
                    Log.Debug("No games with achievements for {ConsoleName}", console.Name);
                }

                await Task.Delay(500); // Rate limiting
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error fetching games for console {ConsoleName}", console.Name);
            }
        }

        return totalGames;
    }

    private static async Task SaveGameDataAsync(List<GameInfo> games, JsonSerializerOptions options)
    {
        try
        {
            Log.Information("Saving {GameCount:N0} games to '{FileName}'...", games.Count, OutputFileNameJson);
            var json = JsonSerializer.Serialize(games, options);
            await File.WriteAllTextAsync(OutputFileNameJson, json);
            Log.Information("JSON file saved successfully");

            Log.Information("Saving {GameCount:N0} games to '{FileName}'...", games.Count, OutputFileNameMsgPack);
            var msgPack = MessagePackSerializer.Serialize(games);
            await File.WriteAllBytesAsync(OutputFileNameMsgPack, msgPack);
            Log.Information("MessagePack file saved successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save game data");
            throw;
        }
    }

    private static async Task<RaSettings> LoadOrPromptSettings()
    {
        var settings = new RaSettings();

        if (File.Exists(SettingsFilePath))
        {
            try
            {
                await using var stream = new FileStream(SettingsFilePath, FileMode.Open, FileAccess.Read);
                using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null // Disable XML resolver for security
                });
                var serializer = new XmlSerializer(typeof(RaSettings));
                // ReSharper disable once NullableWarningSuppressionIsUsed
                settings = (RaSettings)serializer.Deserialize(xmlReader)!;
                Log.Information("Loaded settings for user '{Username}'", settings.Username);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load settings");
                settings = new RaSettings();
            }
        }

        var hasValidSettings = !string.IsNullOrWhiteSpace(settings.Username) &&
                               !string.IsNullOrWhiteSpace(settings.WebApiKey);

        if (hasValidSettings)
        {
            Console.WriteLine($"\nCurrent username: '{settings.Username}'");
            Console.Write("Update credentials? (y/n): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (!string.Equals(response, "y", StringComparison.Ordinal) &&
                !string.Equals(response, "yes", StringComparison.Ordinal))
            {
                return await Task.FromResult(settings);
            }
        }
        else
        {
            Console.WriteLine("\n--- No valid settings found. Please enter your credentials ---");
        }

        Console.Write("Username: ");
        settings.Username = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Web API Key: ");
        settings.WebApiKey = Console.ReadLine()?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.WebApiKey))
        {
            Log.Error("Username and Web API Key cannot be empty");
            Log.CloseAndFlush();
            Environment.Exit(1);
        }

        try
        {
            await using var stream = new FileStream(SettingsFilePath, FileMode.Create, FileAccess.Write);
            await using var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8
            });
            var serializer = new XmlSerializer(typeof(RaSettings));
            serializer.Serialize(xmlWriter, settings);
            Log.Information("Settings saved to {SettingsFilePath}", SettingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not save settings");
        }

        return await Task.FromResult(settings);
    }

    private static async Task SaveConsoleListAsync(List<ConsoleInfo> consoles)
    {
        try
        {
            var lines = consoles.Select(static c => $"{c.Id:D3}: {c.Name}");
            await File.WriteAllLinesAsync(ConsoleListFilePath, lines);
            Log.Information("Console list saved to '{FilePath}' ({Count} entries)", ConsoleListFilePath,
                consoles.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not save console list");
        }
    }
}