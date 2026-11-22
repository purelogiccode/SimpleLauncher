namespace CoverCatalogBuilder;

/// <summary>
///     Builds the canonical cover catalog and 800px JPEG derivatives from a local
///     Simple Launcher images folder. The output is ready for an incremental R2 upload.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        BuilderOptions options;
        try
        {
            options = BuilderOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            BuilderOptions.PrintUsage();
            return 2;
        }

        if (options.ShowHelp)
        {
            BuilderOptions.PrintUsage();
            return 0;
        }

        Console.WriteLine("Simple Launcher cover catalog builder");
        Console.WriteLine($"  source:  {options.SourceDirectory}");
        Console.WriteLine($"  output:  {options.OutputDirectory}");
        Console.WriteLine($"  baseUrl: {options.BaseUrl}");
        Console.WriteLine($"  size:    max {options.MaxSize}px, JPEG q{options.Quality}");
        Console.WriteLine();

        return new CatalogBuilder(options).Run();
    }
}
