using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Services.InjectEmulatorConfig;
using SimpleLauncher.Avalonia.Services.SystemManager;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     ViewModel tests for the emulator config-injection feature.
///     Uses a portable-mode temp emulator dir so Dolphin ini injection stays
///     isolated from the real user AppData. The database is redirected to an
///     isolated temp file (exclusive collection) so system lookups never depend
///     on real user data.
/// </summary>
[Collection(nameof(UsesDatabasePathOverride))]
public class InjectDolphinConfigViewModelTests : IDisposable
{
    private readonly string _fakeExe;
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<IMessageBoxLibraryService> _messageBox = new();
    private readonly SettingsManagerService _settings;
    private readonly string _tempDir;

    public InjectDolphinConfigViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SLTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "User", "Config"));
        _fakeExe = Path.Combine(_tempDir, "Dolphin.exe");
        File.WriteAllText(_fakeExe, "");
        File.WriteAllText(Path.Combine(_tempDir, "portable.txt"), "");
        // DolphinConfigurationService refuses to create a missing Dolphin.ini (sample file
        // is not shipped); pre-create an empty one so injection updates it in place.
        File.WriteAllText(Path.Combine(_tempDir, "User", "Config", "Dolphin.ini"), "");

        var config = new ConfigurationBuilder().Build();
        _settings = new SettingsManagerService(
            config,
            _logger.Object,
            new Mock<ICredentialProtector>().Object,
            _messageBox.Object);

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

    private InjectDolphinConfigViewModel CreateViewModel()
    {
        var resolver = new EmulatorPathResolver(new SystemManagerService(
            new ConfigurationBuilder().Build()));
        return new InjectDolphinConfigViewModel(_settings, _messageBox.Object, resolver, _logger.Object);
    }

    [Fact]
    public void Initialize_LoadsSettingsIntoProperties()
    {
        _settings.Dolphin.GfxBackend = "OpenGL";
        _settings.Dolphin.DspThread = true;
        _settings.Dolphin.ShowSettingsBeforeLaunch = true;

        var vm = CreateViewModel();
        vm.Initialize(_fakeExe, true);

        Assert.Equal("OpenGL", vm.GfxBackend);
        Assert.True(vm.DspThread);
        Assert.True(vm.ShowBeforeLaunch);
        Assert.True(vm.IsLauncherMode);
    }

    [Fact]
    public void Cancel_RaisesCloseRequested()
    {
        var vm = CreateViewModel();
        vm.Initialize(_fakeExe, true);

        var raised = false;
        vm.CloseRequested += (_, _) => raised = true;

        vm.CancelCommand.Execute(null);

        Assert.True(raised);
        Assert.False(vm.ShouldRun);
    }

    [Fact]
    public async Task Save_InjectsPortableIniAndCloses()
    {
        _settings.Dolphin.GfxBackend = "Vulkan";
        _messageBox.Setup(m => m.DolphinConfigurationSavedSuccessfullyMessageBoxAsync()).Returns(Task.CompletedTask);

        var vm = CreateViewModel();
        vm.Initialize(_fakeExe, true);

        var raised = false;
        vm.CloseRequested += (_, _) => raised = true;

        await vm.SaveCommand.ExecuteAsync(null);

        var iniPath = Path.Combine(_tempDir, "User", "Config", "Dolphin.ini");
        Assert.True(File.Exists(iniPath), "Dolphin.ini should have been injected next to the portable emulator");
        Assert.Contains("Vulkan", File.ReadAllText(iniPath), StringComparison.OrdinalIgnoreCase);
        Assert.True(raised, "Save should request window close");
        _messageBox.Verify(m => m.DolphinConfigurationSavedSuccessfullyMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public void GfxBackendOptions_AreAvailable()
    {
        var vm = CreateViewModel();

        Assert.Contains("Vulkan", vm.GfxBackendOptions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("OpenGL", vm.GfxBackendOptions, StringComparer.OrdinalIgnoreCase);
    }
}