using MessagePack;
using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.Services.AvaloniaServices;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.MameData;
using SimpleLauncher.Core.Services.MameManager;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the deferred mame.dat load-failure notification: the MAME data service is
///     constructed before the main window exists, so the constructor must not try to show a
///     dialog; startup calls NotifyLoadFailureIfNeededAsync once the window is shown.
/// </summary>
public class MameDataServiceTests
{
    [Fact]
    public void Constructor_ValidDatLoadsMachinesWithoutNotifying()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mame-valid-{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(path, MessagePackSerializer.Serialize(new List<MameManagerService>
        {
            new() { MachineName = "pacman", Description = "Pac-Man (Midway)" }
        }));
        try
        {
            var messageBox = new Mock<IMessageBoxLibraryService>();
            var service = new MameDataService(TestDependencies.Logger().Object, messageBox.Object, path);

            Assert.Equal(MameDataLoadFailure.None, service.LoadFailure);
            Assert.Single(service.Machines);
            Assert.Equal("Pac-Man (Midway)", service.Lookup["pacman"]);
            messageBox.Verify(m => m.ReinstallSimpleLauncherFileMissingMessageBoxAsync(), Times.Never);
            messageBox.Verify(m => m.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync(), Times.Never);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CorruptDat_ConstructorDefersAndNotifyShowsDialogOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mame-corrupt-{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(path, "not a mame dat"u8.ToArray());
        try
        {
            var messageBox = new Mock<IMessageBoxLibraryService>();
            var logger = TestDependencies.Logger();
            var service = new MameDataService(logger.Object, messageBox.Object, path);

            Assert.Equal(MameDataLoadFailure.CorruptedFile, service.LoadFailure);
            Assert.Empty(service.Machines);

            // A corrupt mame.dat is the user's data failing, not app code (bug 67738):
            // it must never reach the bug-report sink, which collects Warning+ events.
            logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
            logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
            logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
            logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);

            messageBox.Verify(m => m.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync(), Times.Never,
                "the constructor runs before a window exists and must not attempt the dialog");

            await service.NotifyLoadFailureIfNeededAsync();
            messageBox.Verify(m => m.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync(), Times.Once);

            await service.NotifyLoadFailureIfNeededAsync();
            messageBox.Verify(m => m.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync(), Times.Once,
                "the dialog must be shown only once");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MissingDat_ReportsMissingButDoesNotNotify()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mame-missing-{Guid.NewGuid():N}.dat");
        var messageBox = new Mock<IMessageBoxLibraryService>();
        var logger = TestDependencies.Logger();
        var service = new MameDataService(logger.Object, messageBox.Object, path);

        Assert.Equal(MameDataLoadFailure.MissingFile, service.LoadFailure);
        Assert.Empty(service.Machines);

        // A missing mame.dat is an expected user condition (bug 67737): it must never
        // reach the bug-report sink, which collects Warning+ events.
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);

        await service.NotifyLoadFailureIfNeededAsync();
        messageBox.Verify(m => m.ReinstallSimpleLauncherFileMissingMessageBoxAsync(), Times.Never,
            "the required-files check owns the missing-file dialog");
        messageBox.Verify(m => m.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync(), Times.Never);
    }

    [Fact]
    public void LoadFromDat_NotifyUserFalseSuppressesTheImmediateDialog()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mame-corrupt-{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(path, "not a mame dat"u8.ToArray());
        try
        {
            var messageBox = new Mock<IMessageBoxLibraryService>();
            var machines = MameManagerService.LoadFromDat(TestDependencies.Logger().Object, path,
                messageBox.Object, notifyUser: false, out var failure);

            Assert.Empty(machines);
            Assert.Equal(MameDataLoadFailure.CorruptedFile, failure);
            messageBox.Verify(m => m.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync(), Times.Never);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReinstallDialogs_NoWindowAvailable_CompleteWithoutFaulting()
    {
        // Bugs 67737/67739: the fire-and-forget reinstall dialogs faulted with
        // "No window available" when no window existed yet, surfacing as unobserved
        // task exceptions (Error → bug report). With no owner the dialogs must
        // silently no-op instead of faulting.
        var service = new MessageBoxLibraryService(new ThrowingWindowContext(),
            new ConfigurationBuilder().Build(), new LocalizationService());

        var missing = service.ReinstallSimpleLauncherFileMissingMessageBoxAsync();
        var corrupted = service.ReinstallSimpleLauncherFileCorruptedMessageBoxAsync();

        await Task.WhenAll(missing, corrupted);
        Assert.True(missing.IsCompletedSuccessfully);
        Assert.True(corrupted.IsCompletedSuccessfully);
    }

    private sealed class ThrowingWindowContext : IWindowContext
    {
        public IntPtr Handle => IntPtr.Zero;

        public IDispatcherService Dispatcher => throw new NotSupportedException();

        public object PlatformWindow => throw new InvalidOperationException("No window available");

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Activate()
        {
        }
    }
}