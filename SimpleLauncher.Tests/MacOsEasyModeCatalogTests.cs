using System.Xml;
using System.Xml.Serialization;
using SimpleLauncher.Core.Services.EasyMode;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Guards the macOS Apple Silicon Easy Mode catalog. The file is published to R2 and drives
///     the download buttons, so a broken path or a non-macOS archive would only surface on a Mac.
/// </summary>
public class MacOsEasyModeCatalogTests
{
    private const string CatalogRelativePath =
        "SimpleLauncher/Services/EasyMode/Samples/easymode_macos_arm64.xml";

    [Fact]
    public void MacOsArm64Catalog_UsesAppleSiliconDownloadsAndPaths()
    {
        var catalogPath = Path.Combine(FindRepositoryRoot(), CatalogRelativePath);
        Assert.True(File.Exists(catalogPath), $"The macOS Easy Mode catalog was not found: {catalogPath}");

        var manager = Deserialize(catalogPath);
        Assert.NotNull(manager.Systems);
        Assert.InRange(manager.Systems.Count, 60, 100);

        foreach (var system in manager.Systems)
        {
            Assert.False(string.IsNullOrWhiteSpace(system.SystemName));
            Assert.NotNull(system.Emulators);
            Assert.NotNull(system.Emulators.Emulator);

            var emulator = system.Emulators.Emulator;
            Assert.False(string.IsNullOrWhiteSpace(emulator.EmulatorDownloadLink),
                $"{system.SystemName}: missing emulator download link");
            Assert.DoesNotContain(".exe", emulator.EmulatorDownloadLink, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("%BASEFOLDER%/emulators/", emulator.EmulatorLocation, StringComparison.Ordinal);

            if (emulator.EmulatorName.StartsWith("Retroarch", StringComparison.Ordinal))
            {
                Assert.Equal("%BASEFOLDER%/emulators/RetroArch/RetroArch.app/Contents/MacOS/RetroArch",
                    emulator.EmulatorLocation);
                Assert.EndsWith("RetroArch_Metal.dmg", emulator.EmulatorDownloadLink, StringComparison.Ordinal);
                Assert.EndsWith("_libretro.dylib", emulator.CoreLocation, StringComparison.Ordinal);
                Assert.EndsWith("_libretro.dylib.zip", emulator.CoreDownloadLink, StringComparison.Ordinal);
                Assert.Equal("%BASEFOLDER%/emulators/RetroArch/cores/", emulator.CoreDownloadExtractPath);
            }
            else
            {
                Assert.Contains(".app/Contents/MacOS/", emulator.EmulatorLocation, StringComparison.Ordinal);
                Assert.True(
                    emulator.EmulatorDownloadLink.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                    emulator.EmulatorDownloadLink.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase),
                    $"{system.SystemName}: unexpected macOS download type {emulator.EmulatorDownloadLink}");
            }
        }

        var xbox = manager.Systems.Single(s => string.Equals(s.SystemName, "Microsoft Xbox", StringComparison.Ordinal));
        Assert.Equal("Xemu", xbox.Emulators.Emulator.EmulatorName);
        Assert.Equal("%BASEFOLDER%/emulators/Xemu/xemu.app/Contents/MacOS/xemu",
            xbox.Emulators.Emulator.EmulatorLocation);

        var vita = manager.Systems.Single(s => string.Equals(s.SystemName, "Sony PlayStation Vita", StringComparison.Ordinal));
        Assert.Equal("Vita3K", vita.Emulators.Emulator.EmulatorName);
        Assert.EndsWith("Vita3K.app/Contents/MacOS/Vita3K",
            vita.Emulators.Emulator.EmulatorLocation, StringComparison.Ordinal);
    }

    private static EasyModeManager Deserialize(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        var manager = (EasyModeManager?)new XmlSerializer(typeof(EasyModeManager)).Deserialize(reader);
        Assert.NotNull(manager);
        return manager;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SimpleLauncher.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root (no SimpleLauncher.sln above the test output folder).");
    }
}
