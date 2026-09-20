using Moq;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.ContextMenus;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     LB-04 regression tests: taking a screenshot is a Windows-only feature (the Win32 capture
///     code only exists in the net10.0-windows target). In the portable net10.0 build the handler
///     must return quietly with an Information log instead of creating an empty images folder
///     and showing the misleading "error was reported to the developer" dialog.
/// </summary>
public class TakeScreenshotPlatformTests
{
    [Fact]
    public async Task TakeScreenshot_OutsideWindows_LogsInformationWithoutDialogs()
    {
        var logger = TestDependencies.Logger();
        var messageBox = TestDependencies.MessageBox();
        var configuration = TestEnvironment.ConfigurationFromJson("{}");

        var functions = new AvaloniaContextMenuFunctions(
            logger.Object,
            messageBox.Object,
            TestDependencies.PlaySound(TestDependencies.Settings(configuration)),
            new Mock<IMameDataService>().Object,
            configuration,
            new Mock<IFindCoverImageService>().Object,
            new LocalizationService());

        // In the non-Windows build the guard returns before the context is dereferenced.
        await functions.TakeScreenshotOfSelectedWindowAsync(null!);

        logger.Verify(
            l => l.Information(It.Is<string>(s => s.Contains("only supported on Windows", StringComparison.Ordinal))),
            Times.Once);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);

        messageBox.Verify(m => m.ErrorMessageBoxAsync(), Times.Never);
        messageBox.Verify(m => m.TakeScreenShotMessageBoxAsync(), Times.Never);
        messageBox.Verify(m => m.CouldNotSaveScreenshotMessageBoxAsync(), Times.Never);
    }
}
