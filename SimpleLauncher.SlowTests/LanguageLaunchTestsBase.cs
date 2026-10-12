namespace SimpleLauncher.SlowTests;

public static class LanguageLaunchTestsBase
{
    // Mirrors LanguageMenuService.NameToCode (18 selectable languages)
    public static readonly string[] SupportedLanguageCodes =
    [
        "ar", "bn", "de", "en", "es", "fr", "hi", "id", "it",
        "ja", "ko", "nl", "pt-br", "ru", "tr", "ur", "vi", "zh-hans"
    ];

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleLauncher");

    /// <summary>
    ///     Locates the built SimpleLauncher.exe (output of the referenced WPF app project).
    /// </summary>
    public static string FindAppExecutable()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                var candidate = Path.Combine(dir.FullName, "SimpleLauncher", "bin", config, "net10.0-windows",
                    "SimpleLauncher.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        throw new FileNotFoundException("SimpleLauncher.exe not found. Build the WPF project first.");
    }

    /// <summary>
    ///     Counts occurrences of a marker across all current SimpleLauncher log files.
    ///     The app holds its log files open while running, so reads use
    ///     FileShare.ReadWrite and retry transient IO failures.
    /// </summary>
    public static int CountLogMarker(string marker)
    {
        if (!Directory.Exists(LogDirectory)) return 0;

        var count = 0;
        foreach (var file in Directory.GetFiles(LogDirectory, "error_user*.log"))
        {
            var content = ReadFileWithRetry(file);
            if (content is null) continue;

            var idx = 0;
            while ((idx = content.IndexOf(marker, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                idx += marker.Length;
            }
        }

        return count;
    }

    private static string? ReadFileWithRetry(string file)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(fs);
                return reader.ReadToEnd();
            }
            catch (IOException)
            {
                Thread.Sleep(300); // log file momentarily locked by the running app
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(300);
            }
        }

        return null;
    }
}