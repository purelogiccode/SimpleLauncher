using System.Runtime.InteropServices;
using SimpleLauncher.Core.Services.ExtractFiles;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the 7-Zip fallback executable selection in <see cref="ExtractionService" /> —
///     Windows bundles 7za.exe, Linux/macOS bundle the extension-less 7zz binaries.
/// </summary>
public class ExtractionServiceTests
{
    [Theory]
    [InlineData(Architecture.X64, true, "7za.exe")]
    [InlineData(Architecture.Arm64, true, "7za_arm64.exe")]
    [InlineData(Architecture.X64, false, "7zz")]
    [InlineData(Architecture.Arm64, false, "7zz_arm64")]
    public void GetSevenZipExecutableName_PicksBinaryForPlatform(
        Architecture architecture, bool isWindows, string expected)
    {
        Assert.Equal(expected, ExtractionService.GetSevenZipExecutableName(architecture, isWindows));
    }
}
