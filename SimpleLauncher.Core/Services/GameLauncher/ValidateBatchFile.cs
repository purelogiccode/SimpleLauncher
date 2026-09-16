using System.Text.RegularExpressions;

namespace SimpleLauncher.Core.Services.GameLauncher;

/// <summary>
///     Provides methods to validate batch file contents by checking that referenced file paths exist.
/// </summary>
public partial class ValidateBatchFile
{
    private static readonly Regex QuotedPathRegex = MyRegex1();

    /// <summary>
    ///     Reads a batch file and returns a list of quoted paths that do not exist on disk.
    /// </summary>
    public static IList<string> ValidateBatchFileContents(string batchFilePath)
    {
        var missingPaths = new List<string>();

        if (!File.Exists(batchFilePath)) return missingPaths;

        try
        {
            var lines = File.ReadAllLines(batchFilePath);
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (trimmed.StartsWith("rem", StringComparison.OrdinalIgnoreCase)) continue;
                if (trimmed.StartsWith("::", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith('#')) continue;

                foreach (Match match in QuotedPathRegex.Matches(line))
                {
                    var path = match.Groups["path"].Value.Trim();
                    if (string.IsNullOrEmpty(path)) continue;

                    var expanded = Environment.ExpandEnvironmentVariables(path);
                    if (File.Exists(expanded) || Directory.Exists(expanded)) continue;

                    missingPaths.Add(expanded);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[ValidateBatchFile] ValidateBatchFileContents failed: {ex.Message}");
        }

        return missingPaths;
    }

    /// <summary>
    ///     Parses quoted strings from a batch file and returns those that look like paths but do not exist.
    /// </summary>
    public static IList<string> FindInvalidQuotedPathsSimple(string batchFilePath)
    {
        var invalidPaths = new List<string>();

        if (!File.Exists(batchFilePath)) return invalidPaths;

        try
        {
            var content = File.ReadAllText(batchFilePath);
            var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Skip comments exactly like ValidateBatchFileContents: a commented-out
                // path (rem "D:\old\game.exe") must not trigger the missing-paths prompt.
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("rem", StringComparison.OrdinalIgnoreCase)) continue;
                if (trimmed.StartsWith("::", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith('#')) continue;

                var currentIndex = 0;
                while (currentIndex < line.Length)
                {
                    var startQuote = line.IndexOf('"', currentIndex);
                    if (startQuote == -1) break;

                    var endQuote = line.IndexOf('"', startQuote + 1);
                    if (endQuote == -1) break;

                    var quotedText = line.Substring(startQuote + 1, endQuote - startQuote - 1);

                    if (!string.IsNullOrWhiteSpace(quotedText) && LooksLikePath(quotedText))
                    {
                        var expanded = Environment.ExpandEnvironmentVariables(quotedText);
                        if (!File.Exists(expanded) && !Directory.Exists(expanded)) invalidPaths.Add(expanded);
                    }

                    currentIndex = endQuote + 1;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"[ValidateBatchFile] FindInvalidQuotedPathsSimple failed: {ex.Message}");
        }

        return invalidPaths;
    }

    private static bool LooksLikePath(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Check for drive letter pattern (C:\, D:\, etc.)
        if (text.Length >= 3 && char.IsLetter(text[0]) && text[1] == ':' && text[2] == '\\')
            return true;

        // Check for UNC path (\\server\share)
        if (text.StartsWith(@"\\", StringComparison.Ordinal))
            return true;

        // Check if contains backslash (likely a path)
        if (text.Contains('\\'))
            return true;

        return false;
    }

    [GeneratedRegex("""
                    "(?<path>(?:[A-Za-z]:|\\\\)[^"\r\n]+)"
                    """, RegexOptions.Compiled | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex MyRegex1();
}