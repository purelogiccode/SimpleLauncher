using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     The WPF reference is frozen and no longer the version source. Its version metadata is
///     intentionally left at the value it had when the reference was frozen, and this test never
///     rewrites Avalonia/Core/updater files (the old implementation did, which would roll the
///     shipped app back to the reference version).
/// </summary>
public class VersionConsistencyTests
{
    private const string FrozenWpfVersion = "5.9.1";

    private static string GetProjectFilePath(string relativePath)
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyLocation == null)
            throw new InvalidOperationException("Could not determine executing assembly location.");

        var path = Path.Combine(assemblyLocation, "..", "..", "..", "..", relativePath);
        return Path.GetFullPath(path);
    }

    /// <summary>
    ///     Verifies that the frozen WPF reference keeps its historical version instead of being
    ///     auto-synchronized with the canonical Avalonia version.
    /// </summary>
    [Fact]
    public void WpfReferenceVersionIsFrozen()
    {
        var csprojPath = GetProjectFilePath(Path.Combine("SimpleLauncher", "SimpleLauncher.csproj"));
        Assert.True(File.Exists(csprojPath), $"SimpleLauncher.csproj not found at {csprojPath}");

        var doc = XDocument.Load(csprojPath);
        var assemblyVersion = doc.Descendants("AssemblyVersion").FirstOrDefault()?.Value;
        Assert.False(string.IsNullOrWhiteSpace(assemblyVersion),
            "AssemblyVersion not found in SimpleLauncher.csproj");

        Assert.Equal(FrozenWpfVersion, assemblyVersion.Trim());
    }
}
