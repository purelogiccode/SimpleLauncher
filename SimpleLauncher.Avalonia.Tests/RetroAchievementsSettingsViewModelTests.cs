using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Services.RetroAchievements;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.RetroAchievements;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="RetroAchievementsSettingsViewModel" /> (Avalonia) — save, validation,
///     login/token flow, emulator configuration dispatch and error handling.
/// </summary>
public class RetroAchievementsSettingsViewModelTests
{
    private static IConfiguration Config => new ConfigurationBuilder().Build();

    private static (RetroAchievementsSettingsViewModel Vm, SettingsManagerService Settings,
        Mock<IMessageBoxLibraryService> MessageBox,
        Mock<IRetroAchievementsEmulatorConfiguratorService> Configurator, Mock<ILogger> Logger)
        CreateVm(
            Func<HttpRequestMessage, HttpResponseMessage> httpResponder,
            string? initialUsername = "user",
            string? initialApiKey = "key",
            string? initialPassword = "pass",
            string? initialToken = null)
    {
        TestEnvironment.EnsurePortableSettings();
        var logger = new Mock<ILogger>();
        var credProtector = new Mock<ICredentialProtector>();
        credProtector.Setup(p => p.Protect(It.IsAny<string>())).Returns<string>(s => s);
        credProtector.Setup(p => p.Unprotect(It.IsAny<string>())).Returns<string>(s => s);
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var settings = new SettingsManagerService(Config, logger.Object, credProtector.Object, messageBox.Object);
        settings.RaUsername = initialUsername ?? "";
        settings.RaApiKey = initialApiKey ?? "";
        settings.RaPassword = initialPassword ?? "";
        settings.RaToken = initialToken ?? "";

        var rp = TestDependencies.ResourceProvider();
        var httpClient = TestDependencies.HttpClientWith(httpResponder);
        var factory = TestDependencies.HttpFactory(httpClient);
        var manager = new RetroAchievementsManager();
        var raService = new RetroAchievementsService(factory.Object, manager, Config, logger.Object);
        var configurator = new Mock<IRetroAchievementsEmulatorConfiguratorService>();
        var vm = new RetroAchievementsSettingsViewModel(settings, logger.Object, messageBox.Object, raService,
            rp.Object, configurator.Object);
        return (vm, settings, messageBox, configurator, logger);
    }

    private static HttpResponseMessage LoginSuccess(string token = "tok123")
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { Success = true, Token = token }), Encoding.UTF8,
                "application/json")
        };
    }

    private static HttpResponseMessage LoginFailure()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { Success = false }), Encoding.UTF8,
                "application/json")
        };
    }

    [Fact]
    public void Ctor_InitializesFromSettings()
    {
        var (vm, _, _, _, _) = CreateVm(_ => LoginSuccess(), "alice", "k1", "p1");
        Assert.Equal("alice", vm.Username);
        Assert.Equal("k1", vm.ApiKey);
        Assert.Equal("p1", vm.Password);
    }

    [Fact]
    public void Ctor_EmulatorIntegrationSectionMatchesPlatform()
    {
        var (vm, _, _, _, _) = CreateVm(_ => LoginSuccess());

        // The configurator writes the Windows config locations of each emulator, so the
        // section must stay hidden on Linux/macOS (LB-08).
        Assert.Equal(OperatingSystem.IsWindows(), vm.IsEmulatorIntegrationVisible);
    }

    [Fact]
    public async Task SaveCommand_TrimsUsernameAndRaisesSaveCompleted()
    {
        var (vm, settings, _, _, _) = CreateVm(_ => LoginSuccess());
        vm.Username = "  bob  ";
        vm.ApiKey = "newKey";
        vm.Password = "newPass";
        var raised = false;
        vm.SaveCompleted += (_, _) => raised = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("bob", settings.RaUsername);
        Assert.Equal("newKey", settings.RaApiKey);
        Assert.Equal("newPass", settings.RaPassword);
        Assert.True(raised);
    }

    [Fact]
    public void CancelCommand_RaisesCloseRequested()
    {
        var (vm, _, _, _, _) = CreateVm(_ => LoginSuccess());
        var raised = false;
        vm.CloseRequested += (_, _) => raised = true;
        vm.CancelCommand.Execute(null);
        Assert.True(raised);
    }

    [Fact]
    public async Task ConfigureEmulator_EmptyUsernameOrPassword_ShowsEnterUsernamePassword()
    {
        var (vm, _, messageBox, _, _) = CreateVm(_ => LoginSuccess());
        vm.Username = "";
        vm.Password = "";
        await vm.ConfigureEmulatorCommand.ExecuteAsync("RetroArch");
        messageBox.Verify(m => m.EnterUsernamePasswordMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfigureEmulator_WhitespacePassword_ShowsEnterUsernamePassword()
    {
        var (vm, _, messageBox, _, _) = CreateVm(_ => LoginSuccess());
        vm.Username = "user";
        vm.Password = "   ";
        await vm.ConfigureEmulatorCommand.ExecuteAsync("PCSX2");
        messageBox.Verify(m => m.EnterUsernamePasswordMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfigureEmulator_RetroArch_DoesNotRequireToken_DirectlyConfigures()
    {
        var (vm, _, messageBox, configurator, _) = CreateVm(_ => LoginFailure());
        vm.Username = "user";
        vm.Password = "pass";
        configurator.Setup(c => c.ConfigureRetroArch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        vm.RequestExePath = () => Task.FromResult<string?>(@"C:\emu\retroarch.exe");

        await vm.ConfigureEmulatorCommand.ExecuteAsync("RetroArch");

        configurator.Verify(c => c.ConfigureRetroArch(@"C:\emu\retroarch.exe", "user", "pass"), Times.Once);
        messageBox.Verify(m => m.EmulatorConfiguredSuccessfullyMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfigureEmulator_NonRetroArch_MissingToken_FetchesTokenAndSucceeds()
    {
        // ReSharper disable once NotAccessedVariable
        var callCount = 0;
        // ReSharper disable once UnusedParameter.Local
        var (vm, settings, messageBox, configurator, _) = CreateVm(_ =>
        {
            callCount++;
            return LoginSuccess("newToken");
        }, initialApiKey: "", initialToken: "");
        vm.Username = "user";
        vm.Password = "pass";
        configurator.Setup(c => c.ConfigurePcsx2(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        vm.RequestExePath = () => Task.FromResult<string?>(@"C:\emu\pcsx2.exe");

        await vm.ConfigureEmulatorCommand.ExecuteAsync("PCSX2");

        Assert.Equal("newToken", settings.RaToken);
        configurator.Verify(c => c.ConfigurePcsx2(@"C:\emu\pcsx2.exe", "user", "newToken"), Times.Once);
        messageBox.Verify(m => m.EmulatorConfiguredSuccessfullyMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfigureEmulator_MissingToken_LoginFails_ShowsFailedToLogin()
    {
        var (vm, _, messageBox, _, _) = CreateVm(_ => LoginFailure(), initialApiKey: "", initialToken: "");
        vm.Username = "user";
        vm.Password = "pass";
        vm.RequestExePath = () => Task.FromResult<string?>(@"C:\emu\pcsx2.exe");

        await vm.ConfigureEmulatorCommand.ExecuteAsync("PCSX2");

        messageBox.Verify(m => m.FailedToLoginToRetroAchievementsMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.EmulatorConfiguredSuccessfullyMessageBoxAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfigureEmulator_ExistingToken_SkipsLogin()
    {
        var httpCalled = false;
        var (vm, _, messageBox, configurator, _) = CreateVm(_ =>
        {
            httpCalled = true;
            return LoginSuccess();
        }, initialApiKey: "key", initialToken: "existingToken");
        vm.Username = "user";
        vm.Password = "pass";
        configurator.Setup(c => c.ConfigureDuckStation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        vm.RequestExePath = () => Task.FromResult<string?>(@"C:\duck\exe");

        await vm.ConfigureEmulatorCommand.ExecuteAsync("DuckStation");

        Assert.False(httpCalled);
        configurator.Verify(c => c.ConfigureDuckStation(It.IsAny<string>(), "user", "existingToken"), Times.Once);
        messageBox.Verify(m => m.EmulatorConfiguredSuccessfullyMessageBoxAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfigureEmulator_NoExePath_DoesNotConfigure()
    {
        var (vm, _, _, configurator, _) = CreateVm(_ => LoginSuccess(), initialToken: "tok");
        vm.Username = "user";
        vm.Password = "pass";
        vm.RequestExePath = () => Task.FromResult<string?>(null);

        await vm.ConfigureEmulatorCommand.ExecuteAsync("PCSX2");

        configurator.Verify(c => c.ConfigurePcsx2(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfigureEmulator_EmptyExePath_DoesNotConfigure()
    {
        var (vm, _, _, configurator, _) = CreateVm(_ => LoginSuccess(), initialToken: "tok");
        vm.Username = "user";
        vm.Password = "pass";
        vm.RequestExePath = () => Task.FromResult<string?>("");

        await vm.ConfigureEmulatorCommand.ExecuteAsync("Dolphin");

        configurator.Verify(c => c.ConfigureDolphin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfigureEmulator_NoRequestHandler_DoesNotConfigure()
    {
        var (vm, _, _, configurator, _) = CreateVm(_ => LoginSuccess(), initialToken: "tok");
        vm.Username = "user";
        vm.Password = "pass";
        vm.RequestExePath = null;

        await vm.ConfigureEmulatorCommand.ExecuteAsync("PCSX2");

        configurator.Verify(c => c.ConfigurePcsx2(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfigureEmulator_ConfiguratorReturnsFalse_ShowsFailedToConfigure()
    {
        var (vm, _, messageBox, configurator, _) = CreateVm(_ => LoginSuccess(), initialToken: "tok");
        vm.Username = "user";
        vm.Password = "pass";
        configurator.Setup(c => c.ConfigureFlycast(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);
        vm.RequestExePath = () => Task.FromResult<string?>("C:\\flycast.exe");

        await vm.ConfigureEmulatorCommand.ExecuteAsync("Flycast");

        messageBox.Verify(m => m.FailedToConfigureTheEmulatorMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.EmulatorConfiguredSuccessfullyMessageBoxAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfigureEmulator_ConfiguratorThrows_ShowsAnErrorOccurred()
    {
        var (vm, _, messageBox, configurator, _) = CreateVm(_ => LoginSuccess(), initialToken: "tok");
        vm.Username = "user";
        vm.Password = "pass";
        configurator.Setup(c => c.ConfigureBizHawk(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("disk full"));
        vm.RequestExePath = () => Task.FromResult<string?>("C:\\bizhawk.exe");

        await vm.ConfigureEmulatorCommand.ExecuteAsync("BizHawk");

        messageBox.Verify(m => m.AnErrorOccurredWhileConfiguringTheEmulatorMessageBoxAsync(), Times.Once);
        messageBox.Verify(m => m.EmulatorConfiguredSuccessfullyMessageBoxAsync(), Times.Never);
    }

    [Theory]
    [InlineData("PCSX2")]
    [InlineData("Dolphin")]
    [InlineData("PPSSPP")]
    [InlineData("Flycast")]
    [InlineData("BizHawk")]
    public async Task ConfigureEmulator_VariousEmulators_DispatchCorrectly(string emulator)
    {
        var (vm, _, _, configurator, _) = CreateVm(_ => LoginSuccess(), initialToken: "tok");
        vm.Username = "user";
        vm.Password = "pass";
        configurator.Setup(c => c.ConfigurePcsx2(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        configurator.Setup(c => c.ConfigureDolphin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        configurator.Setup(c => c.ConfigurePpspp(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        configurator.Setup(c => c.ConfigureFlycast(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        configurator.Setup(c => c.ConfigureBizHawk(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        configurator.Setup(c => c.ConfigureRetroArch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        configurator.Setup(c => c.ConfigureDuckStation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        vm.RequestExePath = () => Task.FromResult<string?>("C:\\emu.exe");

        await vm.ConfigureEmulatorCommand.ExecuteAsync(emulator);

        Assert.True(configurator.Invocations.Count > 0, $"Expected at least one configurator call for {emulator}");
    }
}