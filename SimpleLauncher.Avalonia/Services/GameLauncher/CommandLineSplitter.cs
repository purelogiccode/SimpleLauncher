using System.Text;

namespace SimpleLauncher.Avalonia.Services.GameLauncher;

/// <summary>
///     Splits an emulator argument string into argv entries using POSIX shell-like quoting
///     rules (single quotes, double quotes, backslash escapes). Used by the Flatpak host
///     launcher so arguments reach the emulator as real argv entries instead of being
///     re-interpreted by a shell (which would expand <c>$</c>/backticks in ROM paths).
/// </summary>
internal static class CommandLineSplitter
{
    /// <summary>
    ///     Splits a command line into arguments. Unmatched quotes are treated literally.
    /// </summary>
    public static IReadOnlyList<string> Split(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return [];

        var arguments = new List<string>();
        var current = new StringBuilder();
        var inSingleQuotes = false;
        var inDoubleQuotes = false;
        var hasToken = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];

            if (inSingleQuotes)
            {
                if (character == '\'') inSingleQuotes = false;
                else current.Append(character);
                continue;
            }

            if (inDoubleQuotes)
            {
                if (character == '"')
                {
                    inDoubleQuotes = false;
                }
                else if (character == '\\' && index + 1 < commandLine.Length &&
                         commandLine[index + 1] is '"' or '\\' or '$' or '`')
                {
                    current.Append(commandLine[++index]);
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (hasToken)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inDoubleQuotes = true;
                    hasToken = true;
                    break;
                case '\'':
                    inSingleQuotes = true;
                    hasToken = true;
                    break;
                case '\\' when index + 1 < commandLine.Length:
                    current.Append(commandLine[++index]);
                    hasToken = true;
                    break;
                default:
                    current.Append(character);
                    hasToken = true;
                    break;
            }
        }

        if (hasToken) arguments.Add(current.ToString());
        return arguments;
    }
}
