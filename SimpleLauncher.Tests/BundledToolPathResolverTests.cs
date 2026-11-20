using System.Runtime.InteropServices;
using SimpleLauncher.Core.Services.BundledTools;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the platform-specific payload resolution in
///     <see cref="BundledToolPathResolver" /> for the helper tools that ship one payload per
///     OS/architecture under tools\ (CHDMounter, RetroAchievementsSharp, SimpleXisoDrive and
///     SimpleZipDrive).
/// </summary>
public class BundledToolPathResolverTests
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

        var runtimeIdentifier = BundledToolPathResolver.GetPlatformRuntimeIdentifier();

        if (expectedArchitecture == null)
        {
            Assert.Null(runtimeIdentifier);
            return;
        }

        Assert.Equal($"win-{expectedArchitecture}", runtimeIdentifier);
    }

    /// <summary>
    ///     Verifies that the helper executable names carry the Windows .exe suffix on Windows.
    /// </summary>
    [Theory]
    [InlineData("CHDMounter")]
    [InlineData("RetroAchievementsSharp.Cli")]
    [InlineData("SimpleXisoDrive")]
    [InlineData("SimpleZipDrive")]
    public void PlatformExecutableNameUsesTheWindowsExeSuffix(string baseName)
    {
        Assert.Equal($"{baseName}.exe", BundledToolPathResolver.GetPlatformExecutableName(baseName));
    }

    /// <summary>
    ///     Guards the csproj copy rules: every per-RID helper must ship its binary for the
    ///     running OS/architecture in the build output (same resolution the app performs at
    ///     runtime).
    /// </summary>
    [Theory]
    [InlineData("CHDMounter", "CHDMounter")]
    [InlineData("RetroAchievementsSharp", "RetroAchievementsSharp.Cli")]
    [InlineData("SimpleXisoDrive", "SimpleXisoDrive")]
    [InlineData("SimpleZipDrive", "SimpleZipDrive")]
    public void PerRidToolPayloadExistsForTheRunningPlatform(string toolFolder, string baseName)
    {
        var runtimeIdentifier = BundledToolPathResolver.GetPlatformRuntimeIdentifier();
        if (runtimeIdentifier == null) return; // unsupported OS/architecture: no payload expected

        var executableName = BundledToolPathResolver.GetPlatformExecutableName(baseName);
        var path = Path.Combine(AppContext.BaseDirectory, "tools", toolFolder, runtimeIdentifier, executableName);

        Assert.True(File.Exists(path), $"The bundled payload is missing for the running platform: {path}");
    }

    /// <summary>
    ///     Verifies the absolute-path helper used by the hashing and mount services.
    /// </summary>
    [Fact]
    public void GetToolExecutablePathReturnsThePerRidPayload()
    {
        var runtimeIdentifier = BundledToolPathResolver.GetPlatformRuntimeIdentifier();
        if (runtimeIdentifier == null) return;

        var path = BundledToolPathResolver.GetToolExecutablePath("SimpleZipDrive", "SimpleZipDrive");

        Assert.NotNull(path);
        Assert.EndsWith(
            Path.Combine("tools", "SimpleZipDrive", runtimeIdentifier,
                BundledToolPathResolver.GetPlatformExecutableName("SimpleZipDrive")),
            path, StringComparison.OrdinalIgnoreCase);
    }
}
