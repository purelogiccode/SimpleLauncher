using MessagePack;
using Moq;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the <see cref="MameMachineData" /> class covering loading from .dat files.
/// </summary>
public class MameManagerTests : IDisposable
{
    private readonly ILogger _logErrors = new NoOpLogger();

    /// <summary>
    ///     Initializes a new instance of the <see cref="MameManagerTests" /> class,
    ///     installing the service provider mock for dependency resolution.
    /// </summary>
    public MameManagerTests()
    {
        ServiceProviderMock.Install();
    }

    /// <summary>
    ///     Restores the service provider mock and suppresses finalization.
    /// </summary>
    public void Dispose()
    {
        ServiceProviderMock.Restore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Verifies that LoadFromDat correctly deserializes a valid MessagePack .dat file.
    /// </summary>
    [Fact]
    public void LoadFromDatValidDatFileReturnsDeserializedList()
    {
        var items = new List<MameMachineData>
        {
            new() { MachineName = "pacman", Description = "Pac-Man (Midway)" },
            new() { MachineName = "mspacman", Description = "Ms. Pac-Man" }
        };

        var bytes = MessagePackSerializer.Serialize(items);
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dat");
        File.WriteAllBytes(tempFile, bytes);

        try
        {
            var result = MameMachineData.LoadFromDat(_logErrors, tempFile);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, static m => m is { MachineName: "pacman", Description: "Pac-Man (Midway)" });
            Assert.Contains(result, static m => m is { MachineName: "mspacman", Description: "Ms. Pac-Man" });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that LoadFromDat correctly deserializes a single-item .dat file.
    /// </summary>
    [Fact]
    public void LoadFromDatSingleItemReturnsCorrectItem()
    {
        var items = new List<MameMachineData>
        {
            new() { MachineName = "galaga", Description = "Galaga (Namco)" }
        };

        var bytes = MessagePackSerializer.Serialize(items);
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dat");
        File.WriteAllBytes(tempFile, bytes);

        try
        {
            var result = MameMachineData.LoadFromDat(_logErrors, tempFile);

            Assert.Single(result);
            Assert.Equal("galaga", result[0].MachineName);
            Assert.Equal("Galaga (Namco)", result[0].Description);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that LoadFromDat returns an empty list for corrupted MessagePack data without
    ///     logging at Warning or Error level: a corrupt mame.dat is the user's data failing,
    ///     not app code (bug 67738), and must never reach the bug-report sink.
    /// </summary>
    [Fact]
    public void LoadFromDatCorruptedMessagePackReturnsEmptyList()
    {
        var logger = new Mock<ILogger>();
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dat");
        File.WriteAllBytes(tempFile, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

        try
        {
            var result = MameMachineData.LoadFromDat(logger.Object, tempFile);

            Assert.NotNull(result);
            Assert.Empty(result);
            logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
            logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
            logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
            logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that LoadFromDat returns an empty list for XML content.
    /// </summary>
    [Fact]
    public void LoadFromDatXmlContentReturnsEmptyList()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dat");
        File.WriteAllText(tempFile, "<xml><item>test</item></xml>");

        try
        {
            var result = MameMachineData.LoadFromDat(_logErrors, tempFile);

            Assert.NotNull(result);
            Assert.Empty(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Verifies that a missing .dat file returns an empty list without logging at Warning
    ///     or Error level: a missing mame.dat is an expected user condition (bug 67737) and
    ///     must never reach the bug-report sink.
    /// </summary>
    [Fact]
    public void LoadFromDatMissingFileReturnsEmptyListWithoutWarningOrError()
    {
        var logger = new Mock<ILogger>();
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dat");

        var result = MameMachineData.LoadFromDat(logger.Object, tempFile);

        Assert.NotNull(result);
        Assert.Empty(result);
        logger.Verify(l => l.Warning(It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
        logger.Verify(l => l.Error(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    ///     Verifies that LoadFromDat returns an empty list for an empty file.
    /// </summary>
    [Fact]
    public void LoadFromDatEmptyFileReturnsEmptyList()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dat");
        File.WriteAllBytes(tempFile, []);

        try
        {
            var result = MameMachineData.LoadFromDat(_logErrors, tempFile);

            Assert.NotNull(result);
            Assert.Empty(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}