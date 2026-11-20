using SimpleLauncher.Core.Services.CheckPaths;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests the shared ZIP/RAR temporary-extraction startup guard used by both apps.
/// </summary>
public class TemporaryExtractionGuardTests
{
    [Fact]
    public void RunningFromTempFolder_IsDetected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"SL_Guard_{Guid.NewGuid():N}");
        var appDirectory = Path.Combine(tempRoot, "SimpleLauncher");

        Assert.True(TemporaryExtractionGuard.IsRunningFromTemporaryExtraction(appDirectory, tempRoot));
    }

    [Fact]
    public void RunningOutsideTempFolder_IsNotDetected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"SL_Guard_{Guid.NewGuid():N}");
        var appDirectory = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!,
            $"SL_Installed_{Guid.NewGuid():N}");

        Assert.False(TemporaryExtractionGuard.IsRunningFromTemporaryExtraction(appDirectory, tempRoot));
    }

    [Fact]
    public void PrefixSiblingFolder_IsNotDetected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"SL_Guard_{Guid.NewGuid():N}");
        var appDirectory = tempRoot + "Other";

        Assert.False(TemporaryExtractionGuard.IsRunningFromTemporaryExtraction(appDirectory, tempRoot));
    }

    [Fact]
    public void TemporaryFolderItself_IsDetected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"SL_Guard_{Guid.NewGuid():N}");

        Assert.True(TemporaryExtractionGuard.IsRunningFromTemporaryExtraction(tempRoot, tempRoot));
    }
}
