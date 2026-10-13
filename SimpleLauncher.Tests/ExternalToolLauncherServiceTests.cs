using System.Runtime.InteropServices;
using SimpleLauncher.Core.Services.ExternalToolLauncher;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the platform-specific payload resolution in
///     <see cref="ExternalToolLauncherService" /> for the bundled tools that ship one payload
///     per OS/architecture under tools\ (XISOStudio, CHDStudio, RVZStudio, CHDMounter,
///     FindRomCover, RetroAchievementsSharp, SimpleXisoDrive and SimpleZipDrive).
/// </summary>
public class ExternalToolLauncherServiceTests
{
    /// <summary>
    ///     Verifies that the resolved tool payload folder matches the running platform.
    ///     The test suite only runs on Windows, so the expected identifier is win-x64/win-arm64.
    /// </summary>
    [Fact]
    public void PlatformRuntimeIdentifierMatchesTheRunningPlatform()
    {
        var expectedArchitecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        var runtimeIdentifier = ExternalToolLauncherService.GetPlatformRuntimeIdentifier();

        if (expectedArchitecture == null)
        {
            Assert.Null(runtimeIdentifier);
            return;
        }

        Assert.Equal($"win-{expectedArchitecture}", runtimeIdentifier);
    }

    /// <summary>
    ///     Verifies that the tool executable names carry the Windows .exe suffix on Windows.
    /// </summary>
    [Theory]
    [InlineData("XISOStudio")]
    [InlineData("CHDStudio")]
    [InlineData("FindRomCover")]
    [InlineData("RetroAchievementsSharp.Cli")]
    [InlineData("SimpleXisoDrive")]
    [InlineData("SimpleZipDrive")]
    public void PlatformExecutableNameUsesTheWindowsExeSuffix(string baseName)
    {
        Assert.Equal($"{baseName}.exe", ExternalToolLauncherService.GetPlatformExecutableName(baseName));
    }

    /// <summary>
    ///     Guards the csproj copy rules: every per-RID tool must ship its binary for the running
    ///     OS/architecture in the build output (same resolution the app performs at runtime).
    /// </summary>
    [Theory]
    [InlineData("FindRomCover", "FindRomCover")]
    [InlineData("RetroAchievementsSharp", "RetroAchievementsSharp.Cli")]
    [InlineData("SimpleXisoDrive", "SimpleXisoDrive")]
    [InlineData("SimpleZipDrive", "SimpleZipDrive")]
    public void PerRidToolPayloadExistsForTheRunningPlatform(string toolFolder, string baseName)
    {
        var runtimeIdentifier = ExternalToolLauncherService.GetPlatformRuntimeIdentifier();
        if (runtimeIdentifier == null) return; // unsupported OS/architecture: no payload expected

        var executableName = ExternalToolLauncherService.GetPlatformExecutableName(baseName);
        var path = Path.Combine(AppContext.BaseDirectory, "tools", toolFolder, runtimeIdentifier, executableName);

        Assert.True(File.Exists(path), $"The bundled payload is missing for the running platform: {path}");
    }
}