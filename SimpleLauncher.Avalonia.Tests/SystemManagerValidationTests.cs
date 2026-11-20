using SimpleLauncher.Avalonia.Services.SystemManager;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Verifies the Avalonia system.xml loader applies the same strict validation as WPF:
///     invalid systems are rejected instead of being silently accepted.
/// </summary>
public class SystemManagerValidationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"SL_SystemValidation_{Guid.NewGuid():N}");

    public SystemManagerValidationTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // ignored
        }
    }

    [Fact]
    public void LoadSystemsFromPath_RejectsSystemsMissingRequiredFields()
    {
        var xmlPath = WriteSystemXml("""
            <SystemConfigs>
              <SystemConfig>
                <SystemName>Valid</SystemName>
                <SystemFolders><SystemFolder>C:\roms</SystemFolder></SystemFolders>
                <SystemImageFolder>C:\images</SystemImageFolder>
                <FileFormatsToSearch><FormatToSearch>zip</FormatToSearch></FileFormatsToSearch>
                <Emulators>
                  <Emulator><EmulatorName>MAME</EmulatorName><EmulatorPath>mame.exe</EmulatorPath></Emulator>
                </Emulators>
              </SystemConfig>
              <SystemConfig>
                <SystemName>No image folder</SystemName>
                <SystemFolders><SystemFolder>C:\roms</SystemFolder></SystemFolders>
                <FileFormatsToSearch><FormatToSearch>zip</FormatToSearch></FileFormatsToSearch>
              </SystemConfig>
              <SystemConfig>
                <SystemName>No formats</SystemName>
                <SystemFolders><SystemFolder>C:\roms</SystemFolder></SystemFolders>
                <SystemImageFolder>C:\images</SystemImageFolder>
              </SystemConfig>
              <SystemConfig>
                <SystemName>Bad extraction</SystemName>
                <SystemFolders><SystemFolder>C:\roms</SystemFolder></SystemFolders>
                <SystemImageFolder>C:\images</SystemImageFolder>
                <FileFormatsToSearch><FormatToSearch>zip</FormatToSearch><FormatToSearch>iso</FormatToSearch></FileFormatsToSearch>
                <ExtractFileBeforeLaunch>true</ExtractFileBeforeLaunch>
                <FileFormatsToLaunch><FormatToLaunch>cue</FormatToLaunch></FileFormatsToLaunch>
              </SystemConfig>
              <SystemConfig>
                <SystemName>Empty emulator</SystemName>
                <SystemFolders><SystemFolder>C:\roms</SystemFolder></SystemFolders>
                <SystemImageFolder>C:\images</SystemImageFolder>
                <FileFormatsToSearch><FormatToSearch>zip</FormatToSearch></FileFormatsToSearch>
                <Emulators>
                  <Emulator><EmulatorName></EmulatorName><EmulatorPath>mame.exe</EmulatorPath></Emulator>
                </Emulators>
              </SystemConfig>
            </SystemConfigs>
            """);

        var manager = new SystemManagerService(TestEnvironment.ConfigurationFromJson("{}"));
        var systems = manager.LoadSystemsFromPath(xmlPath);

        var valid = Assert.Single(systems);
        Assert.Equal("Valid", valid.SystemName);
    }

    [Fact]
    public void LoadSystemsFromPath_AcceptsExtractionOnlyForArchives()
    {
        var xmlPath = WriteSystemXml("""
            <SystemConfigs>
              <SystemConfig>
                <SystemName>Archives</SystemName>
                <SystemFolders><SystemFolder>C:\roms</SystemFolder></SystemFolders>
                <SystemImageFolder>C:\images</SystemImageFolder>
                <FileFormatsToSearch><FormatToSearch>ZIP</FormatToSearch><FormatToSearch>7z</FormatToSearch></FileFormatsToSearch>
                <ExtractFileBeforeLaunch>true</ExtractFileBeforeLaunch>
                <FileFormatsToLaunch><FormatToLaunch>cue</FormatToLaunch></FileFormatsToLaunch>
              </SystemConfig>
            </SystemConfigs>
            """);

        var manager = new SystemManagerService(TestEnvironment.ConfigurationFromJson("{}"));
        var systems = manager.LoadSystemsFromPath(xmlPath);

        Assert.Single(systems);
        Assert.True(systems[0].ExtractFileBeforeLaunch);
    }

    private string WriteSystemXml(string xml)
    {
        var path = Path.Combine(_tempRoot, "system.xml");
        File.WriteAllText(path, xml);
        return path;
    }
}
