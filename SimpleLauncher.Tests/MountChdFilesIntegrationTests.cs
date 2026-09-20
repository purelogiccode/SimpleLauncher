using System.Runtime.InteropServices;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.GameLauncher.MountFiles;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;
using Xunit.Sdk;

namespace SimpleLauncher.Tests;

/// <summary>
///     Integration tests that mount real CHD disc images with CHDMounter and verify the mount succeeds.
///     Tests are skipped at runtime when the CHD file, the CHDMounter tool, or the Dokan driver is unavailable.
/// </summary>
public sealed class MountChdFilesIntegrationTests
{
    private readonly ILogger _logger = new NoOpLogger();
    private readonly IMessageBoxLibraryService _messageBox = new NoOpMessageBoxLibraryService();

    /// <summary>
    ///     Gets the test data rows for CHD mount integration tests, specifying the file path, game name, and console alias.
    /// </summary>
    public static TheoryData<string, string, string> ChdFiles => new()
    {
        // Microsoft Xbox (CHDMounter console alias "xbox")
        { @"E:\Microsoft Xbox\007 - Everything or Nothing (USA).chd", "007 - Everything or Nothing (USA)", "xbox" },
        { @"E:\Microsoft Xbox\4x4 Evo 2 (USA).chd", "4x4 Evo 2 (USA)", "xbox" },
        { @"E:\Microsoft Xbox\007 - Agent Under Fire (USA).chd", "007 - Agent Under Fire (USA)", "xbox" },
        // Sony PlayStation 3 (CHDMounter console alias "ps3")
        { @"F:\Sony PlayStation 3\007 - Blood Stone (USA) (En,Fr).chd", "007 - Blood Stone (USA) (En,Fr)", "ps3" },
        {
            @"F:\Sony PlayStation 3\007 - Quantum of Solace (USA) (En,Fr) (Collector's Edition).chd",
            "007 - Quantum of Solace (USA) (En,Fr) (Collector's Edition)", "ps3"
        },
        { @"F:\Sony PlayStation 3\3D Dot Game Heroes (USA).chd", "3D Dot Game Heroes (USA)", "ps3" },
        // SNK Neo Geo CD (CHDMounter console alias "neogeocd")
        { @"J:\SNK Neo Geo CD\ADK World (Japan).chd", "ADK World (Japan)", "neogeocd" },
        { @"J:\SNK Neo Geo CD\Andro Dunos (France) (Unl).chd", "Andro Dunos (France) (Unl)", "neogeocd" },
        {
            @"J:\SNK Neo Geo CD\2020 Super Baseball (Japan) (En,Ja).chd", "2020 Super Baseball (Japan) (En,Ja)",
            "neogeocd"
        }
    };

    /// <summary>
    ///     Verifies that a real CHD disc image can be mounted and unmounted cleanly, checking that the mounted
    ///     drive is accessible and contains the expected system-specific content.
    /// </summary>
    /// <param name="chdFilePath">The full path to the CHD file to mount.</param>
    /// <param name="gameName">The display name of the game for assertion messages.</param>
    /// <param name="consoleAlias">The CHDMounter console alias identifying the system type.</param>
    [Theory]
    [MemberData(nameof(ChdFiles))]
    public async Task MountRealChdSucceeds_And_UnmountsCleanly(string chdFilePath, string gameName, string consoleAlias)
    {
        if (!File.Exists(chdFilePath)) throw SkipException.ForSkip($"CHD file not found: {chdFilePath}");

#pragma warning disable CA1416
        if (!DokanValidation.IsDokanInstalled())
#pragma warning restore CA1416
            throw SkipException.ForSkip("Dokan driver is not installed. CHD cannot be mounted.");

        var chdMounterExePath =
            Path.Combine(AppContext.BaseDirectory, "tools", "CHDMounter", GetChdMounterExecutableName());
        if (!File.Exists(chdMounterExePath))
            throw SkipException.ForSkip($"CHDMounter executable not found: {chdMounterExePath}");

        var mountService = new MountChdFiles(_logger);

        string? driveRoot;

        await using (var mounted = await mountService.MountAsync(chdFilePath, consoleAlias, _logger, _messageBox))
        {
            Assert.True(mounted.IsMounted, $"Mount failed for '{gameName}' (console alias {consoleAlias}).");

            Assert.False(string.IsNullOrEmpty(mounted.MountedPath), "Mounted path was empty.");
            Assert.False(string.IsNullOrEmpty(mounted.MountedDriveLetter), "Mounted drive letter was empty.");

            driveRoot = $"{mounted.MountedDriveLetter}:\\";
            Assert.True(Directory.Exists(driveRoot), $"Mounted drive {driveRoot} is not accessible.");

            var entries = Directory.GetFileSystemEntries(driveRoot);
            Assert.NotEmpty(entries);

            AssertSystemSpecificContent(consoleAlias, driveRoot, gameName);
        }

        // After DisposeAsync the CHDMounter process is killed and the drive should be gone.
        await WaitForDriveToDisappearAsync(driveRoot);
    }

    private static void AssertSystemSpecificContent(string consoleAlias, string driveRoot, string gameName)
    {
        switch (consoleAlias)
        {
            case "xbox":
                // Xbox XDVDFS discs always contain default.xbe at the root.
                Assert.True(
                    File.Exists(Path.Combine(driveRoot, "default.xbe")),
                    $"default.xbe not found on mounted Xbox CHD '{gameName}'.");
                break;
            case "ps3":
                // PS3 discs always contain a PS3_GAME directory at the root.
                Assert.True(
                    Directory.Exists(Path.Combine(driveRoot, "PS3_GAME")),
                    $"PS3_GAME not found on mounted PS3 CHD '{gameName}'.");
                break;
        }
    }

    private static async Task WaitForDriveToDisappearAsync(string? driveRoot)
    {
        if (string.IsNullOrEmpty(driveRoot)) return;

        const int maxRetries = 20;
        for (var i = 0; i < maxRetries; i++)
        {
            if (!Directory.Exists(driveRoot)) return;

            await Task.Delay(500);
        }

        Assert.False(Directory.Exists(driveRoot), $"Drive {driveRoot} still exists after unmount.");
    }

    private static string GetChdMounterExecutableName()
    {
        return RuntimeInformation.ProcessArchitecture ==
               Architecture.Arm64
            ? "CHDMounter_arm64.exe"
            : "CHDMounter.exe";
    }
}