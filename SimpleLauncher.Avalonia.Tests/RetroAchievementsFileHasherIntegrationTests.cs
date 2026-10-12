using Moq;
using SimpleLauncher.Core.Services.RetroAchievements;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Integration tests for the RetroAchievementsSharp CLI tool-backed file hasher
///     (the bundled single-file binary that replaced the in-process library and the
///     old RAHasher executable). These tests run the real hashing path through the
///     CLI on the current platform.
/// </summary>
public class RetroAchievementsFileHasherIntegrationTests
{
    private static RetroAchievementsFileHasher CreateHasher()
    {
        return new RetroAchievementsFileHasher(
            new Mock<ILogger>().Object,
            new RetroAchievementsSystemMatcher(new Mock<ILogger>().Object));
    }

    [Fact]
    public async Task CalculateHashAsync_GenesisRom_ReturnsValidHash()
    {
        // Integration test: runs the real RetroAchievementsSharp CLI tool
        // against a dummy cartridge file (system 'genesis/mega drive', whole-file MD5).
        var tempFile = Path.Combine(Path.GetTempPath(), "SLTest_rahash_" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(tempFile, new byte[65536]);

            var hasher = CreateHasher();
            var hash = await hasher.CalculateHashAsync(tempFile, "genesis/mega drive");

            Assert.False(string.IsNullOrEmpty(hash), "The hashing engine should produce a hash for a cartridge ROM");
            AssertHashFormat(hash);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CalculateHashAsync_UnsupportedInput_ReturnsNull()
    {
        // A dummy zero-filled file is not a valid disc image, so the engine must
        // fail gracefully (return null) instead of throwing.
        var tempFile = Path.Combine(Path.GetTempPath(), "SLTest_rahash_" + Guid.NewGuid().ToString("N") + ".iso");
        try
        {
            File.WriteAllBytes(tempFile, new byte[65536]);

            var hasher = CreateHasher();
            var hash = await hasher.CalculateHashAsync(tempFile, "playstation");

            Assert.Null(hash);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static void AssertHashFormat(string hash)
    {
        Assert.Equal(32, hash.Length);
        Assert.All(hash, c => Assert.True(Uri.IsHexDigit(c), $"non-hex char in hash: {hash}"));
    }
}