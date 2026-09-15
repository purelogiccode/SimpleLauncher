using System.Text;
using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Services.InjectEmulatorConfig;
using SimpleLauncher.Avalonia.Services.SystemManager;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for EmulatorPathResolver — config-driven via a temporary system.xml,
///     so no real user configuration is touched. The database is redirected to an
///     isolated temp file (exclusive collection) so results never depend on real data.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class EmulatorPathResolverTests : IDisposable
{
    private readonly string _fakeExePath;
    private readonly Mock<ILogger> _logger = new();
    private readonly EmulatorPathResolver _resolver;
    private readonly SystemManagerService _systemManager;
    private readonly string _systemXmlPath;
    private readonly string _tempDir;

    public EmulatorPathResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SLTest_resolver_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _fakeExePath = Path.Combine(_tempDir, "emulators", "Dolphin.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(_fakeExePath)!);
        File.WriteAllText(_fakeExePath, "");

        _systemXmlPath = Path.Combine(_tempDir, "system.xml");

        var config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(
                Encoding.UTF8.GetBytes(
                    $$"""{"SystemXmlPath": "{{_systemXmlPath.Replace("\\", @"\\")}}"}""")))
            .Build();

        _systemManager = new SystemManagerService(config);
        _resolver = new EmulatorPathResolver(_systemManager);

        UnifiedTestDatabase.RedirectToTempDb(_tempDir);
    }

    public void Dispose()
    {
        UnifiedTestDatabase.ClearRedirect();
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
            // best effort cleanup
        }
    }

    /// <summary>
    ///     Writes a system.xml with one system carrying the given emulators,
    ///     then seeds the redirected test database from it.
    /// </summary>
    private void WriteSystemXml(params (string Name, string Path)[] emulators)
    {
        var emulatorXml = string.Join("\n", emulators.Select(e =>
            $"        <Emulator><EmulatorName>{e.Name}</EmulatorName><EmulatorPath>{e.Path}</EmulatorPath></Emulator>"));
        File.WriteAllText(_systemXmlPath, $"""
                                           <?xml version="1.0" encoding="utf-8"?>
                                           <SystemConfigs>
                                               <SystemConfig>
                                                   <SystemName>GameCube</SystemName>
                                                   <Emulators>
                                           {emulatorXml}
                                                   </Emulators>
                                               </SystemConfig>
                                           </SystemConfigs>
                                           """);
        UnifiedTestDatabase.SeedSystemsFromXml(_systemManager, _systemXmlPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryFindEmulatorPath_NullOrWhitespaceHint_ReturnsNull(string? hint)
    {
        WriteSystemXml(("Dolphin 5.0", _fakeExePath));

        var result = _resolver.TryFindEmulatorPath(hint!, _logger.Object);

        Assert.Null(result);
    }

    [Fact]
    public void TryFindEmulatorPath_NoSystemXml_ReturnsNull()
    {
        // system.xml path does not exist yet — LoadSystems returns an empty list
        var result = _resolver.TryFindEmulatorPath("Dolphin", _logger.Object);

        Assert.Null(result);
    }

    [Fact]
    public void TryFindEmulatorPath_MatchingEmulator_ReturnsResolvedPath()
    {
        WriteSystemXml(("Dolphin 5.0", _fakeExePath));

        var result = _resolver.TryFindEmulatorPath("Dolphin", _logger.Object);

        Assert.Equal(_fakeExePath, result);
    }

    [Fact]
    public void TryFindEmulatorPath_IsCaseInsensitive()
    {
        WriteSystemXml(("Dolphin 5.0", _fakeExePath));

        var result = _resolver.TryFindEmulatorPath("dolphin", _logger.Object);

        Assert.Equal(_fakeExePath, result);
    }

    [Fact]
    public void TryFindEmulatorPath_ExeMissing_ReturnsNull()
    {
        WriteSystemXml(("Dolphin 5.0", Path.Combine(_tempDir, "missing", "Dolphin.exe")));

        var result = _resolver.TryFindEmulatorPath("Dolphin", _logger.Object);

        Assert.Null(result);
    }

    [Fact]
    public void TryFindEmulatorPath_NoNameMatch_ReturnsNull()
    {
        WriteSystemXml(("Dolphin 5.0", _fakeExePath));

        var result = _resolver.TryFindEmulatorPath("Yuzu", _logger.Object);

        Assert.Null(result);
    }

    [Fact]
    public void TryFindEmulatorPath_RelativeLocation_ResolvesAgainstAppDirectory()
    {
        // Relative emulator path — resolved against the test's base directory
        var relativeExe = Path.Combine("tools", "emulators", "dolphin-test.exe");
        var fullExe = Path.Combine(AppContext.BaseDirectory, relativeExe);
        Directory.CreateDirectory(Path.GetDirectoryName(fullExe)!);
        try
        {
            File.WriteAllText(fullExe, "");

            WriteSystemXml(("Dolphin 5.0", relativeExe));

            var result = _resolver.TryFindEmulatorPath("Dolphin", _logger.Object);

            Assert.Equal(Path.GetFullPath(fullExe), result);
        }
        finally
        {
            File.Delete(fullExe);
        }
    }

    [Fact]
    public void TryFindEmulatorPath_EmptyLocation_SkipsEmulator()
    {
        WriteSystemXml(("Dolphin 5.0", ""), ("Dolphin Portable", _fakeExePath));

        var result = _resolver.TryFindEmulatorPath("Dolphin", _logger.Object);

        // The empty-location emulator is skipped; the second match wins.
        Assert.Equal(_fakeExePath, result);
    }

    [Fact]
    public void TryFindEmulatorPath_MultipleSystems_SecondSystemMatchFound()
    {
        // Two systems: first with a non-matching emulator, second with the match.
        File.WriteAllText(_systemXmlPath, $"""
                                           <?xml version="1.0" encoding="utf-8"?>
                                           <SystemConfigs>
                                                <SystemConfig>
                                                    <SystemName>Switch</SystemName>
                                                    <Emulators>
                                                        <Emulator><EmulatorName>Yuzu</EmulatorName><EmulatorPath>{_fakeExePath}</EmulatorPath></Emulator>
                                                    </Emulators>
                                                </SystemConfig>
                                               <SystemConfig>
                                                   <SystemName>GameCube</SystemName>
                                                   <Emulators>
                                                       <Emulator><EmulatorName>Dolphin 5.0</EmulatorName><EmulatorPath>{_fakeExePath}</EmulatorPath></Emulator>
                                                   </Emulators>
                                               </SystemConfig>
                                           </SystemConfigs>
                                           """);
        UnifiedTestDatabase.SeedSystemsFromXml(_systemManager, _systemXmlPath);

        var result = _resolver.TryFindEmulatorPath("Dolphin", _logger.Object);

        Assert.Equal(_fakeExePath, result);
    }
}