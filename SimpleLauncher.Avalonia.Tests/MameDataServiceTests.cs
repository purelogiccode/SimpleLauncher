using MessagePack;
using Moq;
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
            var service = new MameDataService(TestDependencies.Logger().Object, messageBox.Object, path);

            Assert.Equal(MameDataLoadFailure.CorruptedFile, service.LoadFailure);
            Assert.Empty(service.Machines);
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
        var service = new MameDataService(TestDependencies.Logger().Object, messageBox.Object, path);

        Assert.Equal(MameDataLoadFailure.MissingFile, service.LoadFailure);
        Assert.Empty(service.Machines);

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
}
