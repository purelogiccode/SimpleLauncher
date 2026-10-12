using System.Runtime.InteropServices;
using SimpleLauncher.Core.Services.ExternalToolLauncher;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the platform-specific payload resolution in
///     <see cref="ExternalToolLauncherService" /> for the bundled tools that ship one payload
///     per OS/architecture under tools\ (XISOStudio and CHDStudio).
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
    public void PlatformExecutableNameUsesTheWindowsExeSuffix(string baseName)
    {
        Assert.Equal($"{baseName}.exe", ExternalToolLauncherService.GetPlatformExecutableName(baseName));
    }
}
