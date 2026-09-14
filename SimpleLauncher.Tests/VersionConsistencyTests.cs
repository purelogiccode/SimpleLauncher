using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Ensures that version metadata scattered across the repository stays in sync
///     with the canonical version defined in SimpleLauncher.csproj.
///     When a mismatch is detected the test automatically rewrites the file and
///     then fails so the developer can review the change before committing.
/// </summary>
public class VersionConsistencyTests
{
    private static string GetProjectFilePath(string relativePath)
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyLocation == null)
            throw new InvalidOperationException("Could not determine executing assembly location.");

        var path = Path.Combine(assemblyLocation, "..", "..", "..", "..", relativePath);
        return Path.GetFullPath(path);
    }

    private static string GetProjectVersion()
    {
        var csprojPath = GetProjectFilePath(Path.Combine("SimpleLauncher", "SimpleLauncher.csproj"));
        Assert.True(File.Exists(csprojPath), $"SimpleLauncher.csproj not found at {csprojPath}");

        var doc = XDocument.Load(csprojPath);
        var assemblyVersion = doc.Descendants("AssemblyVersion").FirstOrDefault()?.Value;
        Assert.False(string.IsNullOrWhiteSpace(assemblyVersion), "AssemblyVersion not found in SimpleLauncher.csproj");

        return assemblyVersion.Trim();
    }

    /// <summary>
    ///     Verifies that the version in app.manifest matches the AssemblyVersion from the project file, auto-correcting
    ///     mismatches.
    /// </summary>
    [Fact]
    public void AppManifestVersionMatchesProjectVersion()
    {
        AssertManifestVersionMatchesProjectVersion(Path.Combine("SimpleLauncher", "app.manifest"));
    }

    /// <summary>
    ///     Verifies that the version in the Avalonia app.manifest matches the canonical project
    ///     version, auto-correcting mismatches.
    /// </summary>
    [Fact]
    public void AvaloniaAppManifestVersionMatchesProjectVersion()
    {
        AssertManifestVersionMatchesProjectVersion(Path.Combine("SimpleLauncher.Avalonia", "app.manifest"));
    }

    /// <summary>
    ///     Verifies that the Avalonia csproj version metadata matches the canonical version from
    ///     SimpleLauncher.csproj, auto-correcting mismatches.
    /// </summary>
    [Fact]
    public void AvaloniaProjectVersionMatchesProjectVersion()
    {
        AssertProjectVersionMatchesProjectVersion(
            Path.Combine("SimpleLauncher.Avalonia", "SimpleLauncher.Avalonia.csproj"));
    }

    /// <summary>
    ///     Verifies that the Avalonia updater csproj version metadata matches the canonical version
    ///     from SimpleLauncher.csproj, auto-correcting mismatches.
    /// </summary>
    [Fact]
    public void AvaloniaUpdaterProjectVersionMatchesProjectVersion()
    {
        AssertProjectVersionMatchesProjectVersion(
            Path.Combine("SimpleLauncher.Avalonia.Updater", "SimpleLauncher.Avalonia.Updater.csproj"));
    }

    /// <summary>
    ///     Verifies that the WPF updater csproj version metadata matches the canonical version from
    ///     SimpleLauncher.csproj, auto-correcting mismatches.
    /// </summary>
    [Fact]
    public void WpfUpdaterProjectVersionMatchesProjectVersion()
    {
        AssertProjectVersionMatchesProjectVersion(
            Path.Combine("SimpleLauncher.Updater", "SimpleLauncher.Updater.csproj"));
    }

    private static void AssertProjectVersionMatchesProjectVersion(string relativePath)
    {
        var projectVersion = GetProjectVersion();

        var csprojPath = GetProjectFilePath(relativePath);
        Assert.True(File.Exists(csprojPath), $"{relativePath} not found at {csprojPath}");

        var doc = XDocument.Load(csprojPath);
        var versionElements = doc.Descendants()
            .Where(element => element.Name.LocalName is "AssemblyVersion" or "FileVersion" or "Version")
            .ToList();

        var mismatched = versionElements
            .Where(element => !string.Equals(element.Value.Trim(), projectVersion, StringComparison.Ordinal))
            .ToList();
        if (mismatched.Count == 0) return;

        foreach (var element in mismatched) element.Value = projectVersion;
        doc.Save(csprojPath);

        Assert.Fail(
            $"{relativePath} version metadata was automatically updated to '{projectVersion}'. " +
            "Please review the change and commit it.");
    }

    private static void AssertManifestVersionMatchesProjectVersion(string relativePath)
    {
        var projectVersion = GetProjectVersion();
        var version = Version.Parse(projectVersion);

        // app.manifest requires a four-part version number
        var expectedVersion = new Version(
            version.Major,
            version.Minor,
            version.Build == -1 ? 0 : version.Build,
            version.Revision == -1 ? 0 : version.Revision).ToString();

        var manifestPath = GetProjectFilePath(relativePath);
        Assert.True(File.Exists(manifestPath), $"app.manifest not found at {manifestPath}");

        var doc = XDocument.Load(manifestPath);
        XNamespace ns = "urn:schemas-microsoft-com:asm.v1";
        var identityElement = doc.Root?.Element(ns + "assemblyIdentity");
        Assert.NotNull(identityElement);

        var currentVersion = identityElement.Attribute("version")?.Value;
        Assert.False(string.IsNullOrWhiteSpace(currentVersion),
            "assemblyIdentity version attribute not found in app.manifest");

        if (string.Equals(currentVersion, expectedVersion, StringComparison.Ordinal)) return;

        identityElement.SetAttributeValue("version", expectedVersion);
        doc.Save(manifestPath);

        Assert.Fail(
            $"{relativePath} version was automatically updated from '{currentVersion}' to '{expectedVersion}'. " +
            "Please review the change and commit it.");
    }

    /// <summary>
    ///     Verifies that the SimpleLauncher.Updater/version.txt content matches the project version, auto-correcting
    ///     mismatches.
    /// </summary>
    [Fact]
    public void UpdaterVersionTxtMatchesProjectVersion()
    {
        var projectVersion = GetProjectVersion();
        var expectedContent = $"release{projectVersion}";

        var versionTxtPath = GetProjectFilePath(Path.Combine("SimpleLauncher.Updater", "version.txt"));
        Assert.True(File.Exists(versionTxtPath), $"SimpleLauncher.Updater/version.txt not found at {versionTxtPath}");

        var currentContent = File.ReadAllText(versionTxtPath).Trim();
        if (string.Equals(currentContent, expectedContent, StringComparison.Ordinal)) return;

        File.WriteAllText(versionTxtPath, expectedContent + Environment.NewLine);
        Assert.Fail(
            $"SimpleLauncher.Updater/version.txt was automatically updated from '{currentContent}' to '{expectedContent}'. " +
            "Please review the change and commit it.");
    }
}