using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Services.SystemManager;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests XML persistence of SystemManagerService configurations including loading, saving, updating, renaming, and
///     deleting system entries. The unified database is redirected to a missing temp file so these tests always
///     exercise the legacy XML paths, even on machines that already have a real settings.dat.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class SystemManagerXmlPersistenceTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly string _systemXmlPath;
    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemManagerXmlPersistenceTests" /> class,
    ///     creating a temporary test directory and configuration for XML persistence tests.
    /// </summary>
    public SystemManagerXmlPersistenceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_SystemXmlTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _systemXmlPath = Path.Combine(_testDirectory, "system.xml");

        // Keep the legacy XML paths under test and never touch a real settings.dat.
        UnifiedTestDatabase.RedirectToMissingDb(_testDirectory);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SystemXmlPath"] = _systemXmlPath
            })
            .Build();

        ServiceProviderMock.Install(_configuration);
        ResetSystemXmlStaticState();
    }

    /// <summary>
    ///     Cleans up the temporary test directory and restores the service provider mock.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        UnifiedTestDatabase.ClearRedirect();
        ServiceProviderMock.Restore();
        GC.SuppressFinalize(this);
    }

    private static void ResetSystemXmlStaticState()
    {
        var type = typeof(SystemManagerService);
        type.GetField("_fileLocation", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var localFile = Path.Combine(localAppData, "SimpleLauncher", "system.xml");
        try
        {
            if (File.Exists(localFile)) File.Delete(localFile);
        }
        catch
        {
            // ignored
        }
    }

    private static string BuildValidSystemXml()
    {
        var doc = new XDocument(
            new XElement("SystemConfigs",
                new XElement("SystemConfig",
                    new XElement("SystemName", "Arcade"),
                    new XElement("SystemFolders",
                        new XElement("SystemFolder", @"C:\roms\Arcade")),
                    new XElement("SystemImageFolder", @"C:\images\Arcade"),
                    new XElement("FileFormatsToSearch",
                        new XElement("FormatToSearch", ".zip")),
                    new XElement("GroupByFolder", false),
                    new XElement("DisableRecursiveSearch", false),
                    new XElement("FileFormatsToLaunch",
                        new XElement("FormatToLaunch", ".zip")),
                    new XElement("Emulators",
                        new XElement("Emulator",
                            new XElement("EmulatorName", "MAME"),
                            new XElement("EmulatorLocation", @"C:\emu\mame.exe"),
                            new XElement("EmulatorParameters", "-rompath %SYSTEMFOLDER%"),
                            new XElement("ReceiveANotificationOnEmulatorError", true))
                    )
                )
            )
        );
        return doc.ToString();
    }

    private static string BuildMultiSystemXml()
    {
        var doc = new XDocument(
            new XElement("SystemConfigs",
                new XElement("SystemConfig",
                    new XElement("SystemName", "Arcade"),
                    new XElement("SystemFolders",
                        new XElement("SystemFolder", @"C:\roms\Arcade")),
                    new XElement("SystemImageFolder", @"C:\images\Arcade"),
                    new XElement("FileFormatsToSearch",
                        new XElement("FormatToSearch", ".zip")),
                    new XElement("GroupByFolder", false),
                    new XElement("DisableRecursiveSearch", false),
                    new XElement("FileFormatsToLaunch",
                        new XElement("FormatToLaunch", ".zip")),
                    new XElement("Emulators",
                        new XElement("Emulator",
                            new XElement("EmulatorName", "MAME"),
                            new XElement("EmulatorLocation", @"C:\emu\mame.exe"),
                            new XElement("EmulatorParameters", ""),
                            new XElement("ReceiveANotificationOnEmulatorError", true))
                    )
                ),
                new XElement("SystemConfig",
                    new XElement("SystemName", "NES"),
                    new XElement("SystemFolders",
                        new XElement("SystemFolder", @"C:\roms\NES")),
                    new XElement("SystemImageFolder", @"C:\images\NES"),
                    new XElement("FileFormatsToSearch",
                        new XElement("FormatToSearch", ".nes"),
                        new XElement("FormatToSearch", ".zip")),
                    new XElement("GroupByFolder", true),
                    new XElement("DisableRecursiveSearch", true),
                    new XElement("FileFormatsToLaunch",
                        new XElement("FormatToLaunch", ".nes")),
                    new XElement("Emulators",
                        new XElement("Emulator",
                            new XElement("EmulatorName", "RetroArch"),
                            new XElement("EmulatorLocation", @"C:\emu\retroarch.exe"),
                            new XElement("EmulatorParameters", "-L nestopia %ROM%"),
                            new XElement("ReceiveANotificationOnEmulatorError", false)),
                        new XElement("Emulator",
                            new XElement("EmulatorName", "Mesen"),
                            new XElement("EmulatorLocation", @"C:\emu\mesen.exe"),
                            new XElement("EmulatorParameters", "%ROM%"),
                            new XElement("ReceiveANotificationOnEmulatorError", true))
                    )
                )
            )
        );
        return doc.ToString();
    }

    /// <summary>
    ///     Verifies that SystemExists returns false when no XML file exists.
    /// </summary>
    [Fact]
    public void SystemExistsReturnsFalseWhenXmlFileDoesNotExist()
    {
        var result = SystemManagerService.SystemExists("Arcade", _configuration);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that SystemExists returns true for a system present in the XML.
    /// </summary>
    [Fact]
    public void SystemExistsReturnsTrueWhenSystemIsInXml()
    {
        File.WriteAllText(_systemXmlPath, BuildValidSystemXml());
        ResetSystemXmlStaticState();

        var result = SystemManagerService.SystemExists("Arcade", _configuration);
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that SystemExists returns false for a system not in the XML.
    /// </summary>
    [Fact]
    public void SystemExistsReturnsFalseWhenSystemIsNotInXml()
    {
        File.WriteAllText(_systemXmlPath, BuildValidSystemXml());
        ResetSystemXmlStaticState();

        var result = SystemManagerService.SystemExists("NES", _configuration);
        Assert.False(result);
    }

    /// <summary>
    ///     Verifies that system name lookup is case-insensitive.
    /// </summary>
    [Fact]
    public void SystemExistsIsCaseInsensitive()
    {
        File.WriteAllText(_systemXmlPath, BuildValidSystemXml());
        ResetSystemXmlStaticState();

        var result = SystemManagerService.SystemExists("aRcAdE", _configuration);
        Assert.True(result);
    }

    /// <summary>
    ///     Verifies that loading returns an empty list when no XML file exists.
    /// </summary>
    [Fact]
    public void LoadSystemManagersReturnsEmptyListWhenXmlFileDoesNotExist()
    {
        var result = SystemManagerService.LoadSystemManagers(_configuration);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    ///     Verifies that loading triggers creation of the XML file if it does not exist.
    /// </summary>
    [Fact]
    public void LoadSystemManagersCreatesXmlFileWhenFileDoesNotExist()
    {
        SystemManagerService.LoadSystemManagers(_configuration);
        Assert.True(File.Exists(_systemXmlPath));
    }

    /// <summary>
    ///     Verifies that a single system configuration is loaded with all properties intact.
    /// </summary>
    [Fact]
    public void LoadSystemManagersLoadsSingleSystemCorrectly()
    {
        ResetSystemXmlStaticState();
        File.WriteAllText(_systemXmlPath, BuildValidSystemXml());

        var result = SystemManagerService.LoadSystemManagers(_configuration);

        Assert.Single(result);
        var system = result[0];
        Assert.Equal("Arcade", system.SystemName);
        Assert.Single(system.SystemFolders);
        Assert.Equal(@"C:\roms\Arcade", system.SystemFolders[0]);
        Assert.Equal(@"C:\images\Arcade", system.SystemImageFolder);
        Assert.Single(system.FileFormatsToSearch);
        Assert.Equal(".zip", system.FileFormatsToSearch[0]);
        Assert.False(system.ExtractFileBeforeLaunch);
        Assert.False(system.GroupByFolder);
        Assert.False(system.DisableRecursiveSearch);
        Assert.Single(system.FileFormatsToLaunch);
        Assert.Equal(".zip", system.FileFormatsToLaunch[0]);
        Assert.Single(system.Emulators);
        Assert.Equal("MAME", system.Emulators[0].EmulatorName);
    }

    /// <summary>
    ///     Verifies that multiple system configurations are loaded correctly with proper ordering.
    /// </summary>
    [Fact]
    public void LoadSystemManagersLoadsMultipleSystemsCorrectly()
    {
        ResetSystemXmlStaticState();
        File.WriteAllText(_systemXmlPath, BuildMultiSystemXml());

        var result = SystemManagerService.LoadSystemManagers(_configuration);

        Assert.Equal(2, result.Count);

        var arcade = result[0];
        Assert.Equal("Arcade", arcade.SystemName);
        Assert.Single(arcade.Emulators);

        var nes = result[1];
        Assert.Equal("NES", nes.SystemName);
        Assert.Equal(2, nes.Emulators.Count);
        Assert.Equal("RetroArch", nes.Emulators[0].EmulatorName);
        Assert.Equal("Mesen", nes.Emulators[1].EmulatorName);
        Assert.True(nes.GroupByFolder);
        Assert.True(nes.DisableRecursiveSearch);
        Assert.False(nes.Emulators[0].ReceiveANotificationOnEmulatorError);
        Assert.Equal(".nes", nes.FileFormatsToLaunch[0]);
        Assert.Equal(2, nes.FileFormatsToSearch.Count);
    }

    /// <summary>
    ///     Verifies that saving a new system creates the XML file and persists the configuration.
    /// </summary>
    [Fact]
    public async Task SaveSystemConfigurationAsyncAddsNewSystem()
    {
        var system = new SystemManagerService
        {
            SystemName = "Genesis",
            SystemFolders = [@"C:\roms\Genesis"],
            SystemImageFolder = @"C:\images\Genesis",
            FileFormatsToSearch = [".md", ".bin"],
            FileFormatsToLaunch = [".md"],
            Emulators =
            [
                new Emulator
                {
                    EmulatorName = "Blastem",
                    EmulatorLocation = @"C:\emu\blastem.exe",
                    EmulatorParameters = "%ROM%",
                    ReceiveANotificationOnEmulatorError = true
                }
            ]
        };

        await SystemManagerService.SaveSystemConfigurationAsync(system);

        Assert.True(File.Exists(_systemXmlPath));

        // Verify the saved content
        ResetSystemXmlStaticState();
        var loaded = SystemManagerService.LoadSystemManagers(_configuration);

        Assert.Single(loaded);
        Assert.Equal("Genesis", loaded[0].SystemName);
        Assert.Single(loaded[0].SystemFolders);
        Assert.Equal(@"C:\roms\Genesis", loaded[0].SystemFolders[0]);
        Assert.Equal(@"C:\images\Genesis", loaded[0].SystemImageFolder);
        Assert.Equal(2, loaded[0].FileFormatsToSearch.Count);
        Assert.Single(loaded[0].FileFormatsToLaunch);
        Assert.Single(loaded[0].Emulators);
        Assert.Equal("Blastem", loaded[0].Emulators[0].EmulatorName);
    }

    /// <summary>
    ///     Verifies that a new system is added alongside existing systems in the XML.
    /// </summary>
    [Fact]
    public async Task SaveSystemConfigurationAsyncAddsSystemAlongsideExisting()
    {
        await File.WriteAllTextAsync(_systemXmlPath, BuildValidSystemXml());
        ResetSystemXmlStaticState();

        var newSystem = new SystemManagerService
        {
            SystemName = "Genesis",
            SystemFolders = [@"C:\roms\Genesis"],
            SystemImageFolder = @"C:\images\Genesis",
            FileFormatsToSearch = [".md"],
            FileFormatsToLaunch = [".md"],
            Emulators =
            [
                new Emulator
                {
                    EmulatorName = "Blastem",
                    EmulatorLocation = @"C:\emu\blastem.exe",
                    EmulatorParameters = "%ROM%",
                    ReceiveANotificationOnEmulatorError = true
                }
            ]
        };

        await SystemManagerService.SaveSystemConfigurationAsync(newSystem);

        ResetSystemXmlStaticState();
        var loaded = SystemManagerService.LoadSystemManagers(_configuration);

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, static s => string.Equals(s.SystemName, "Arcade", StringComparison.Ordinal));
        Assert.Contains(loaded, static s => string.Equals(s.SystemName, "Genesis", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Verifies that saving with an existing system name updates the configuration.
    /// </summary>
    [Fact]
    public async Task SaveSystemConfigurationAsyncUpdatesExistingSystem()
    {
        await File.WriteAllTextAsync(_systemXmlPath, BuildValidSystemXml());
        ResetSystemXmlStaticState();

        var updatedSystem = new SystemManagerService
        {
            SystemName = "Arcade",
            SystemFolders = [@"C:\roms\Arcade", @"D:\backup\Arcade"],
            SystemImageFolder = @"C:\images\ArcadeUpdated",
            FileFormatsToSearch = [".zip", ".7z"],
            FileFormatsToLaunch = [".zip"],
            Emulators =
            [
                new Emulator
                {
                    EmulatorName = "RetroArch",
                    EmulatorLocation = @"C:\emu\retroarch.exe",
                    EmulatorParameters = "-L mame %ROM%",
                    ReceiveANotificationOnEmulatorError = false
                }
            ]
        };

        await SystemManagerService.SaveSystemConfigurationAsync(updatedSystem);

        ResetSystemXmlStaticState();
        var loaded = SystemManagerService.LoadSystemManagers(_configuration);

        Assert.Single(loaded);
        Assert.Equal("Arcade", loaded[0].SystemName);
        Assert.Equal(2, loaded[0].SystemFolders.Count);
        Assert.Equal(@"C:\images\ArcadeUpdated", loaded[0].SystemImageFolder);
        Assert.Single(loaded[0].Emulators);
        Assert.Equal("RetroArch", loaded[0].Emulators[0].EmulatorName);
        Assert.False(loaded[0].Emulators[0].ReceiveANotificationOnEmulatorError);
    }

    /// <summary>
    ///     Verifies that saving with an originalSystemName parameter renames the system in the XML.
    /// </summary>
    [Fact]
    public async Task SaveSystemConfigurationAsyncRenamesSystem()
    {
        await File.WriteAllTextAsync(_systemXmlPath, BuildValidSystemXml());
        ResetSystemXmlStaticState();

        var renamedSystem = new SystemManagerService
        {
            SystemName = "ArcadeRenamed",
            SystemFolders = [@"C:\roms\Arcade"],
            SystemImageFolder = @"C:\images\Arcade",
            FileFormatsToSearch = [".zip"],
            FileFormatsToLaunch = [".zip"],
            Emulators =
            [
                new Emulator
                {
                    EmulatorName = "MAME",
                    EmulatorLocation = @"C:\emu\mame.exe",
                    EmulatorParameters = "",
                    ReceiveANotificationOnEmulatorError = true
                }
            ]
        };

        // originalSystemName = "Arcade" tells SaveSystemConfigurationAsync to update the old name
        await SystemManagerService.SaveSystemConfigurationAsync(renamedSystem, "Arcade");

        ResetSystemXmlStaticState();
        var loaded = SystemManagerService.LoadSystemManagers(_configuration);

        Assert.Single(loaded);
        Assert.Equal("ArcadeRenamed", loaded[0].SystemName);
    }

    /// <summary>
    ///     Verifies that deleting a system removes it from the XML while preserving others.
    /// </summary>
    [Fact]
    public async Task DeleteSystemAsyncRemovesSystem()
    {
        await File.WriteAllTextAsync(_systemXmlPath, BuildMultiSystemXml());
        ResetSystemXmlStaticState();

        await SystemManagerService.DeleteSystemAsync("Arcade");

        ResetSystemXmlStaticState();
        var loaded = SystemManagerService.LoadSystemManagers(_configuration);

        Assert.Single(loaded);
        Assert.Equal("NES", loaded[0].SystemName);
    }

    /// <summary>
    ///     Verifies that deleting a non-existent system leaves the XML unchanged.
    /// </summary>
    [Fact]
    public async Task DeleteSystemAsyncDoesNothingWhenSystemNotFound()
    {
        await File.WriteAllTextAsync(_systemXmlPath, BuildValidSystemXml());
        ResetSystemXmlStaticState();

        await SystemManagerService.DeleteSystemAsync("NonExistent");

        ResetSystemXmlStaticState();
        var loaded = SystemManagerService.LoadSystemManagers(_configuration);

        Assert.Single(loaded);
        Assert.Equal("Arcade", loaded[0].SystemName);
    }
}