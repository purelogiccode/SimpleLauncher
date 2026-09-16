using System.Diagnostics;
using System.Globalization;
using System.Text;
using Serilog.Events;
using SimpleLauncher.ResourceTranslator.Models;
using SimpleLauncher.ResourceTranslator.Services;
using SimpleLauncher.ResourceTranslator.Services.DebugAndBugReport;

namespace SimpleLauncher.ResourceTranslator;

/// <summary>
///     Entry point for the SimpleLauncher Resource Translator tool.
/// </summary>
public static class Program
{
    private const int BatchSize = 40;

    /// <summary>
    ///     Asynchronous entry point for the application.
    /// </summary>
    private static async Task MainAsync()
    {
        Console.OutputEncoding = Encoding.UTF8;

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
            Log.Information("Simple Launcher Resource Translator");
            Log.Information("===================================");
            Console.WriteLine();

            // Prompt for API key (not stored)
            Console.Write("Enter your OpenRouter API key: ");
            var apiKey = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Error("API key is required");
                Environment.Exit(1);
            }

            Console.WriteLine();

            // Model selection
            var models = OpenRouterTranslationService.GetAvailableModels();
            Log.Information("Available OpenRouter models:");
            for (var i = 0; i < models.Count; i++)
            {
                var marker = string.Equals(models[i].Id, "z-ai/glm-5.3-flash", StringComparison.Ordinal)
                    ? " (default)"
                    : "";
                Console.WriteLine($"  {i + 1}. {models[i].Name} - {models[i].Description}{marker}");
            }

            Console.WriteLine();
            Console.Write("Select model number (press Enter for default): ");
            var modelInput = Console.ReadLine()?.Trim();

            OpenRouterModelInfo selectedModel;
            if (string.IsNullOrEmpty(modelInput) ||
                !int.TryParse(modelInput, CultureInfo.InvariantCulture, out var modelIndex) ||
                modelIndex < 1 || modelIndex > models.Count)
            {
                selectedModel = models.First(static m =>
                    string.Equals(m.Id, "z-ai/glm-5.3-flash", StringComparison.Ordinal));
                Log.Information("Using default model: {ModelName}", selectedModel.Name);
            }
            else
            {
                selectedModel = models[modelIndex - 1];
                Log.Information("Selected model: {ModelName}", selectedModel.Name);
            }

            Console.WriteLine();

            var translator = new OpenRouterTranslationService(apiKey, selectedModel.Id);
            var overallStopwatch = Stopwatch.StartNew();
            var totalTranslated = 0;
            var totalDuplicatesRemoved = 0;

            // Process the shared localization packs (used by both the WPF and Avalonia apps)
            var localizationResourcesPath = FindLocalizationResourcesPath();
            if (localizationResourcesPath != null)
            {
                Log.Information("--- Shared Localization Packs (SimpleLauncher.Core\\Localization) ---");
                var (translated, duplicates) = await ProcessProject(
                    localizationResourcesPath,
                    "strings.en.json",
                    JsonResourceAnalyzer.ReadEnglishKeys,
                    JsonResourceAnalyzer.AnalyzeAllLanguages,
                    JsonResourceWriter.UpdateResourceFile,
                    translator);

                totalTranslated += translated;
                totalDuplicatesRemoved += duplicates;
            }
            else
            {
                Log.Warning(
                    "Could not locate SimpleLauncher.Core/Localization directory. Nothing to translate");
            }

            overallStopwatch.Stop();
            Log.Information("===================================");
            Log.Information("Translation complete!");
            Log.Information("Total time: {Minutes:D2}:{Seconds:D2}", overallStopwatch.Elapsed.Minutes,
                overallStopwatch.Elapsed.Seconds);
            Log.Information("Total keys translated: {TotalTranslated}", totalTranslated);
            if (totalDuplicatesRemoved > 0)
                Log.Information("Total duplicates removed: {TotalDuplicates}", totalDuplicatesRemoved);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled error occurred during translation");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task<(int translated, int duplicatesRemoved)> ProcessProject(
        string resourcesPath,
        string englishFileName,
        Func<string, IDictionary<string, string>> readEnglishKeys,
        Func<string, IDictionary<string, string>, IList<MissingKeyBatch>> analyzeAllLanguages,
        Action<string, IDictionary<string, string>> updateResourceFile,
        OpenRouterTranslationService translator)
    {
        var englishFile = Path.Combine(resourcesPath, englishFileName);
        if (!File.Exists(englishFile))
        {
            Log.Error("English resource file not found: {FilePath}", englishFile);
            return (0, 0);
        }

        var englishKeys = readEnglishKeys(englishFile);
        Log.Information("English base file loaded: {KeyCount} keys", englishKeys.Count);

        var batches = analyzeAllLanguages(resourcesPath, englishKeys);

        if (batches.Count == 0)
        {
            Console.WriteLine();
            Log.Information("All language files are fully synchronized with English. No action needed");
            return (0, 0);
        }

        var totalMissing = batches.Sum(static b => b.MissingKeys.Count);
        var totalDuplicates = batches.Sum(static b => b.DuplicateKeysRemoved.Count);

        Console.WriteLine();
        Log.Information("Analysis Results:");
        Log.Information("  Languages needing updates: {BatchCount}", batches.Count);
        Log.Information("  Total missing keys: {TotalMissing}", totalMissing);
        if (totalDuplicates > 0)
            Log.Information("  Total duplicate keys to remove: {TotalDuplicates}", totalDuplicates);
        Console.WriteLine();

        foreach (var batch in batches)
        {
            Log.Information(
                "  [{LanguageCode}] {LanguageName}: {MissingCount} missing, {DuplicateCount} duplicates",
                batch.LanguageCode, batch.LanguageName, batch.MissingKeys.Count, batch.DuplicateKeysRemoved.Count);
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to proceed with translation, or Ctrl+C to cancel...");
        Console.ReadKey(true);
        Console.WriteLine();

        var totalTranslated = 0;
        var totalDuplicatesRemoved = 0;

        foreach (var batch in batches)
        {
            Log.Information("Processing [{LanguageCode}] {LanguageName}...", batch.LanguageCode,
                batch.LanguageName);
            var languageStopwatch = Stopwatch.StartNew();

            var allTranslations = new Dictionary<string, string>(StringComparer.Ordinal);
            var missingList = batch.MissingKeys;
            var totalBatches = (int)Math.Ceiling(missingList.Count / (double)BatchSize);

            for (var i = 0; i < missingList.Count; i += BatchSize)
            {
                var currentBatch = missingList.Skip(i).Take(BatchSize).ToList();
                var batchNumber = (i / BatchSize) + 1;

                Console.Write($"  Batch {batchNumber}/{totalBatches} ({currentBatch.Count} keys)... ");
                var sw = Stopwatch.StartNew();

                try
                {
                    var translations = await translator.TranslateBatchAsync(batch.LanguageName, currentBatch);
                    foreach (var kvp in translations) allTranslations[kvp.Key] = kvp.Value;

                    Console.WriteLine($"done in {sw.ElapsedMilliseconds}ms");
                    Log.Debug("Batch {BatchNumber} completed in {ElapsedMs}ms", batchNumber,
                        sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Batch {BatchNumber} failed for {LanguageName}", batchNumber,
                        batch.LanguageName);
                    Console.WriteLine($"FAILED: {ex.Message}");
                    Console.WriteLine("  This batch was skipped and will not be written to the resource file.");
                }

                // Small delay to avoid rate limits
                if (i + BatchSize < missingList.Count) await Task.Delay(500);
            }

            // Write back to resource file
            updateResourceFile(batch.FilePath, allTranslations);

            languageStopwatch.Stop();
            Log.Information("Written {TranslationCount} entries to {FileName} in {ElapsedMs}ms",
                allTranslations.Count, Path.GetFileName(batch.FilePath), languageStopwatch.ElapsedMilliseconds);
            Console.WriteLine();

            totalTranslated += allTranslations.Count;
            totalDuplicatesRemoved += batch.DuplicateKeysRemoved.Count;
        }

        return (totalTranslated, totalDuplicatesRemoved);
    }

    /// <summary>
    ///     Synchronous entry point that invokes the asynchronous main method.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        MainAsync().GetAwaiter().GetResult();
    }

    private static string? FindLocalizationResourcesPath()
    {
        // If running from the project directory (development)
        var devPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SimpleLauncher.Core",
            "Localization");
        if (Directory.Exists(devPath))
        {
            var fullPath = Path.GetFullPath(devPath);
            if (File.Exists(Path.Combine(fullPath, "strings.en.json")))
                return fullPath;
        }

        // If running from output near the SimpleLauncher.Core project
        var nearProject = Path.Combine(AppContext.BaseDirectory, "..", "..", "SimpleLauncher.Core", "Localization");
        if (Directory.Exists(nearProject))
        {
            var fullPath = Path.GetFullPath(nearProject);
            if (File.Exists(Path.Combine(fullPath, "strings.en.json")))
                return fullPath;
        }

        // Search upward for the SimpleLauncher.Core folder
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "SimpleLauncher.Core", "Localization");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "strings.en.json")))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}