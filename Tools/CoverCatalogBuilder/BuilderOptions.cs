namespace CoverCatalogBuilder;

/// <summary>Command-line options for the cover catalog builder.</summary>
internal sealed class BuilderOptions
{
    /// <summary>Gets the local images folder to scan.</summary>
    public string SourceDirectory { get; private set; } = "";

    /// <summary>Gets the staging folder that receives the catalog and derivatives.</summary>
    public string OutputDirectory { get; private set; } = "";

    /// <summary>Gets the public URL written into <c>catalog.json</c>.</summary>
    public string BaseUrl { get; private set; } =
        "https://assets.purelogiccode.com/Simple%20Launcher/covers/v1";

    /// <summary>Gets the maximum width or height of a derivative.</summary>
    public int MaxSize { get; private set; } = 800;

    /// <summary>Gets the maximum number of images encoded in parallel.</summary>
    public int Parallelism { get; private set; } = Math.Min(8, Environment.ProcessorCount);

    /// <summary>Gets whether existing derivatives are reused.</summary>
    public bool SkipExisting { get; private set; } = true;

    /// <summary>Gets whether stale derivatives in a system output folder are removed.</summary>
    public bool Clean { get; private set; }

    /// <summary>Gets the optional system-name filter.</summary>
    public List<string> Systems { get; } = [];

    /// <summary>Gets the systems excluded from the build.</summary>
    public List<string> ExcludedSystems { get; } = [];

    /// <summary>Gets the optional per-system group limit (diagnostics only).</summary>
    public int Limit { get; private set; }

    /// <summary>Gets whether the usage text was requested.</summary>
    public bool ShowHelp { get; private set; }

    /// <summary>Parses the command line.</summary>
    /// <param name="args">The process arguments.</param>
    /// <returns>The parsed options.</returns>
    public static BuilderOptions Parse(string[] args)
    {
        var options = new BuilderOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--source":
                    options.SourceDirectory = RequireValue(args, ref index);
                    break;
                case "--output":
                    options.OutputDirectory = RequireValue(args, ref index);
                    break;
                case "--base-url":
                    options.BaseUrl = RequireValue(args, ref index).TrimEnd('/');
                    break;
                case "--systems":
                    options.Systems.AddRange(SplitList(RequireValue(args, ref index)));
                    break;
                case "--exclude-system":
                    options.ExcludedSystems.AddRange(SplitList(RequireValue(args, ref index)));
                    break;
                case "--max-size":
                    options.MaxSize = ParseInt(RequireValue(args, ref index), "--max-size", 64, 4096);
                    break;
                case "--parallel":
                    options.Parallelism = ParseInt(RequireValue(args, ref index), "--parallel", 1, 64);
                    break;
                case "--limit":
                    options.Limit = ParseInt(RequireValue(args, ref index), "--limit", 1, int.MaxValue);
                    break;
                case "--force":
                    options.SkipExisting = false;
                    break;
                case "--skip-existing":
                    options.SkipExisting = true;
                    break;
                case "--clean":
                    options.Clean = true;
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}", nameof(args));
            }
        }

        if (options.ShowHelp) return options;

        if (string.IsNullOrWhiteSpace(options.SourceDirectory))
            throw new ArgumentException("Missing required argument: --source <images folder>.", nameof(args));
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new ArgumentException("Missing required argument: --output <staging folder>.", nameof(args));

        options.SourceDirectory = Path.GetFullPath(options.SourceDirectory);
        options.OutputDirectory = Path.GetFullPath(options.OutputDirectory);

        if (!Directory.Exists(options.SourceDirectory))
            throw new ArgumentException($"Source folder does not exist: {options.SourceDirectory}", nameof(args));

        if (string.Equals(options.SourceDirectory, options.OutputDirectory, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The output folder must be different from the source folder.", nameof(args));

        var wantsWindows = options.Systems.Any(static name =>
            string.Equals(name, "Microsoft Windows", StringComparison.OrdinalIgnoreCase));
        var excludesWindows = options.ExcludedSystems.Any(static name =>
            string.Equals(name, "Microsoft Windows", StringComparison.OrdinalIgnoreCase));
        if (!wantsWindows && !excludesWindows)
        {
            // Microsoft Windows covers are already served per-game from the bucket root
            // (Microsoft Windows/*.png) by the store scanner, so they are not duplicated
            // in the retro cover catalog unless explicitly requested.
            options.ExcludedSystems.Add("Microsoft Windows");
        }

        return options;
    }

    /// <summary>Prints the command-line usage.</summary>
    public static void PrintUsage()
    {
        Console.WriteLine("""
                          Usage:
                            CoverCatalogBuilder --source <images folder> --output <staging folder> [options]

                          Options:
                            --source <path>          Simple Launcher images folder (required)
                            --output <path>          staging folder for catalog + derivatives (required)
                            --base-url <url>         public catalog base URL
                            --systems <a,b>          only build these systems (repeatable)
                            --exclude-system <a,b>   skip these systems (repeatable)
                            --max-size <px>          derivative longest side (default 800)
                            --parallel <n>           images encoded in parallel (default min(8, CPUs))
                            --limit <n>              max groups per system (diagnostics only)
                            --force                  re-encode existing derivatives
                            --clean                  remove stale image files from the output system folders
                            --help                   show this help

                          Output:
                            <output>/catalog.json
                            <output>/<system-slug>/index.json
                            <output>/<system-slug>/<canonical-name>.png
                          """);
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {args[index]}.", nameof(args));

        index++;
        return args[index];
    }

    private static int ParseInt(string value, string option, int minimum, int maximum)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum || parsed > maximum)
        {
            throw new ArgumentException($"{option} must be an integer between {minimum} and {maximum}.",
                nameof(value));
        }

        return parsed;
    }

    private static IEnumerable<string> SplitList(string value)
    {
        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => item.Length > 0);
    }
}
