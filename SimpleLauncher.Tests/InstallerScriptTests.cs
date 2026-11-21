using System.Reflection;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Guards the installer contract that ships next to the portable ZIP:
///     per-user install, .NET 11 runtime bootstrap, and an uninstaller that keeps user data.
///     These are static checks of the Inno Setup script and packaging defines; the
///     installer itself is compiled and smoke-tested by the packaging workflow.
/// </summary>
public class InstallerScriptTests
{
    private static string ReadRepoFile(string relativePath)
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyLocation == null)
            throw new InvalidOperationException("Could not determine executing assembly location.");

        var path = Path.GetFullPath(Path.Combine(assemblyLocation, "..", "..", "..", "..", relativePath));
        Assert.True(File.Exists(path), $"{relativePath} not found at {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void InstallerIsPerUserAndBootstrapsDotNet11()
    {
        var script = ReadRepoFile(Path.Combine("installer", "SimpleLauncher.iss"));
        var packageScript = ReadRepoFile(Path.Combine("scripts", "package-release.ps1"));

        Assert.Contains("DefaultDirName={localappdata}\\SimpleLauncher", script, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequired=lowest", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesAllowed={#ArchitecturesAllowed}", script, StringComparison.Ordinal);
        Assert.Contains("RuntimePageUrl", script, StringComparison.Ordinal);
        Assert.Contains("'11.*'", script, StringComparison.Ordinal);
        Assert.Contains("dotnet-runtime-11.0*-win-", script, StringComparison.Ordinal);
        Assert.Contains("dotnet-runtime-11.0-win-$runtimeArch.exe", packageScript, StringComparison.Ordinal);
        Assert.Contains("https://aka.ms/dotnet/11.0/dotnet-runtime-win-$runtimeArch.exe",
            packageScript, StringComparison.Ordinal);
    }

    [Fact]
    public void UninstallerKeepsUserData()
    {
        var script = ReadRepoFile(Path.Combine("installer", "SimpleLauncher.iss"));

        // Inno removes only the files it installed. Never add a blanket
        // [UninstallDelete] of {app}: settings.dat, system.xml, logs and game
        // media live there too.
        Assert.DoesNotContain("[UninstallDelete]", script, StringComparison.Ordinal);
        Assert.Contains("settings.dat", script, StringComparison.Ordinal);
        Assert.Contains("system.xml", script, StringComparison.Ordinal);
        Assert.Contains("AppMutex=SimpleLauncher_SingleInstanceMutex_", script, StringComparison.Ordinal);

        // Upgrading from the former side-by-side bundle must not leave a stale
        // Avalonia executable or its shortcut launchable.
        Assert.Contains("[InstallDelete]", script, StringComparison.Ordinal);
        Assert.Contains("SimpleLauncher.Avalonia.exe", script, StringComparison.Ordinal);
        Assert.Contains("Simple Launcher (Avalonia UI).lnk", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerAssetNameIsUsedByPackagingWorkflowAndSite()
    {
        var packageScript = ReadRepoFile(Path.Combine("scripts", "package-release.ps1"));
        var releaseWorkflow = ReadRepoFile(Path.Combine(".github", "workflows", "release.yml"));
        var landingScript = ReadRepoFile(Path.Combine("docs", "landing.js"));

        Assert.Contains("-BuildInstaller", packageScript, StringComparison.Ordinal);
        Assert.Contains("SimpleLauncher-${Version}-${Rid}-setup.exe", packageScript, StringComparison.Ordinal);
        Assert.Contains("SimpleLauncher-$($env:VERSION)-win-x64-setup.exe", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("SimpleLauncher-$($env:VERSION)-win-arm64-setup.exe", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("win-x64-setup", landingScript, StringComparison.Ordinal);
        Assert.Contains("win-arm64-setup", landingScript, StringComparison.Ordinal);
    }
}
