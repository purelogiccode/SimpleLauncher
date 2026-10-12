using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="FuseValidation" /> — the Linux FUSE availability probe used before
///     the CHDMounter mount attempt. This suite runs on Windows, where FUSE is never available,
///     so the probe must return false instead of throwing.
/// </summary>
public class FuseValidationTests
{
    [Fact]
    public void IsFuseAvailableReturnsFalseOnWindows()
    {
        Assert.False(FuseValidation.IsFuseAvailable());
    }
}
