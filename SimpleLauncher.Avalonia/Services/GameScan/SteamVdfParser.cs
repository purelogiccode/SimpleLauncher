using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Services.GameScan;

/// <summary>
///     A simple parser for Valve's KeyValue (VDF) file format.
/// </summary>
internal partial class SteamVdfParser : ISteamVdfParser
{
    // Improved regex to handle escaped quotes within strings: "some \"value\" here"
    private static readonly Regex TokenRegex = MyRegex();

    /// <summary>
    ///     Parses a Valve KeyValue (VDF) file into a nested dictionary of key/value pairs.
    /// </summary>
    /// <param name="filePath">The path to the VDF file to parse.</param>
    /// <param name="logErrors">The error logger used when parsing fails.</param>
    /// <param name="logger">The fallback logger used when logErrors is not provided.</param>
    /// <returns>A dictionary representing the parsed VDF content.</returns>
    public IDictionary<string, object> Parse(string filePath, ILogger? logErrors = null, ILogger? logger = null)
    {
        try
        {
            if (!File.Exists(filePath))
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length == 0) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<Dictionary<string, object>>();
            stack.Push(result);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("//", StringComparison.Ordinal) || string.IsNullOrEmpty(trimmedLine))
                    continue;

                if (trimmedLine.StartsWith('{'))
                    continue;

                if (trimmedLine.StartsWith('}'))
                {
                    if (stack.Count > 1)
                        stack.Pop();
                    continue;
                }

                // Use manual unescaping instead of Regex.Unescape to avoid RegexParseException on file paths
                var tokens = TokenRegex.Matches(trimmedLine)
                    .Select(static m => UnescapeVdfValue(m.Groups[1].Value))
                    .ToList();

                if (tokens.Count == 0)
                    continue;

                var currentDict = stack.Peek();

                if (tokens.Count > 1)
                {
                    var key = tokens[0];
                    var value = tokens[1];
                    currentDict[key] = value;
                }
                else
                {
                    var key = tokens[0];
                    var newDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    currentDict[key] = newDict;
                    stack.Push(newDict);
                }
            }

            return result;
        }
        catch (UnauthorizedAccessException)
        {
            // File is locked or inaccessible (e.g. another process has it open)
            // Return empty result without logging — this is expected in some environments
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            // File is locked or unreadable
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            if (logErrors != null)
                logErrors.Error(ex, $"[SteamVdfParser] Failed to parse VDF file: {filePath}");
            else
                logger?.Error(ex, $"[SteamVdfParser] Failed to parse VDF file: {filePath}");

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     Manually unescapes VDF specific sequences.
    ///     This avoids crashing on standard Windows paths like "resource\DearEsther".
    /// </summary>
    private static string UnescapeVdfValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // VDF primarily uses backslashes to escape double quotes and other backslashes.
        // We handle the most common ones. If a backslash is followed by an unrecognized
        // character (like \D in a path), we treat the backslash as a literal.
        return value
            .Replace("\\\"", "\"")
            .Replace(@"\\", "\\")
            .Replace("\\n", "\n")
            .Replace("\\t", "\t");
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0023:UseRegexOptionsExplicitCapture",
        Justification = "Capturing group is needed to extract the VDF value")]
    [GeneratedRegex("\"((?:\\\\.|[^\\\\\"])*)\"", RegexOptions.Compiled, 1000)]
    private static partial Regex MyRegex();
}