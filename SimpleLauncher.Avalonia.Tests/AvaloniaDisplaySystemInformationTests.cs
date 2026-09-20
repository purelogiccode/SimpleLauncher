using SimpleLauncher.Avalonia.Services.DisplaySystemInfo;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the selected system's configuration summary lines
///     (WPF DisplaySystemInformation text-block parity): the hint line, the
///     configuration lines, one block per emulator, and red-flagging of invalid
///     folder/emulator paths.
/// </summary>
public class AvaloniaDisplaySystemInformationTests
{
    private static SystemManagerConfig CreateConfig(string folder, string emulatorPath)
    {
        return new SystemManagerConfig
        {
            SystemName = "NES",
            SystemFolders = [folder],
            SystemImageFolder = "",
            FileFormatsToSearch = [".nes"],
            FileFormatsToLaunch = [".nes"],
            Emulators = new List<Emulator>
            {
                new()
                {
                    EmulatorName = "Mesen",
                    EmulatorLocation = emulatorPath,
                    EmulatorParameters = "\"{rom}\"",
                    ReceiveANotificationOnEmulatorError = true
                }
            }
        };
    }

    [Fact]
    public void BuildSystemInfoLines_MatchesWpfSectionOrder()
    {
        var temp = Directory.CreateTempSubdirectory("sl_displayinfo_");
        try
        {
            var config = CreateConfig(temp.FullName, Path.Combine(temp.FullName, "mesen.exe"));

            var lines = new AvaloniaDisplaySystemInformation().BuildSystemInfoLines(config)
                    .ConvertAll(static l => l.Text)
                ;

            Assert.StartsWith("Click on the letter buttons above", lines[0], StringComparison.Ordinal);
            Assert.Contains(lines, l => l.StartsWith("System Folder:", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("System Image Folder:", StringComparison.Ordinal));
            Assert.Contains(lines,
                l => l.StartsWith("Extension to Search in the System Folder:", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("Extract File Before Launch?:", StringComparison.Ordinal));
            Assert.Contains(lines,
                l => l.StartsWith("Extension to Launch After Extraction:", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("Group Files by Folder?:", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("Disable recursive search:", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("Emulator Name: Mesen", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("Emulator Path:", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("Emulator Parameters:", StringComparison.Ordinal));
            Assert.Contains(lines,
                l => l.StartsWith("Receive a Notification on Emulator Error?:", StringComparison.Ordinal));
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Fact]
    public void BuildSystemInfoLines_FlagsInvalidFoldersAndEmulatorPaths()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var config = CreateConfig(missing, Path.Combine(missing, "mesen.exe"));

        var lines = new AvaloniaDisplaySystemInformation().BuildSystemInfoLines(config);

        Assert.Contains(lines, l => l.IsError && l.Text.StartsWith("System Folder:", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.IsError && l.Text.StartsWith("Emulator Path:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildSystemInfoLines_NoErrorsForValidPaths()
    {
        var temp = Directory.CreateTempSubdirectory("sl_displayinfo_");
        try
        {
            var emulatorPath = Path.Combine(temp.FullName, "mesen.exe");
            File.WriteAllText(emulatorPath, "fake");
            var config = CreateConfig(temp.FullName, emulatorPath);

            var lines = new AvaloniaDisplaySystemInformation().BuildSystemInfoLines(config);

            Assert.DoesNotContain(lines, static l => l.IsError);
        }
        finally
        {
            temp.Delete(true);
        }
    }
}