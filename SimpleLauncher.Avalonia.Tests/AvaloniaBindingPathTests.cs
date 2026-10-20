using System.Text.RegularExpressions;
using SimpleLauncher.Avalonia.Tests.TestHelpers;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression guard for the 5.8.1 "Loading system..." hang: the game-card DataTemplate
///     used runtime type casts in binding paths (for example
///     <c>((vm:MainViewModel)DataContext).CardWidth</c>). Avalonia's reflection binding
///     resolver cannot resolve those casts inside deferred templates, so realizing the first
///     card threw on the UI thread and the loading overlay never went away.
///     Binding paths must use <c>DataContext.Property</c> and let reflection resolve the
///     property on the actual ViewModel instance.
/// </summary>
public class AvaloniaBindingPathTests
{
    [Fact]
    public void XamlBindings_DoNotUseRuntimeTypeCasts()
    {
        var projectDir = AvaloniaProjectPathHelper.GetAvaloniaProjectPath();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(projectDir, "*.axaml", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file)) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Only binding expressions matter here; a cast elsewhere in XAML is not
                // parsed by Avalonia's binding path resolver.
                if (!line.Contains("{Binding", StringComparison.Ordinal) &&
                    !line.Contains("Path=", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Regex.IsMatch(line, @"\(\([A-Za-z_][\w.]*:[A-Za-z_][\w.]*\)", RegexOptions.None,
                        TimeSpan.FromSeconds(1)))
                {
                    violations.Add($"{Path.GetFileName(file)}:{i + 1}: {line.Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "XAML binding paths must not use runtime type casts; use the DataContext path instead:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static bool IsBuildOutput(string file)
    {
        var separator = Path.DirectorySeparatorChar;
        return file.Contains($"{separator}bin{separator}", StringComparison.Ordinal) ||
               file.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }
}
