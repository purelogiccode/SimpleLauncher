using Microsoft.Extensions.Configuration;
using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;
using SimpleLauncher.Services.GameLauncher.Strategies;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for the <see cref="DosBoxLaunchStrategy" /> class.
/// </summary>
public class DosBoxLaunchStrategyTests
{
    private static DosBoxLaunchStrategy CreateStrategy()
    {
        var extractionServiceMock = new Mock<IExtractionService>();
        var configurationMock = new Mock<IConfiguration>();
        var messageBoxMock = new Mock<IMessageBoxLibraryService>();
        var mountChdFilesMock = new Mock<IMountChdFiles>();
        var mountIsoFilesMock = new Mock<IMountIsoFiles>();
        var discConverterMock = new Mock<IDiscConverter>();
        var debugLoggerMock = new Mock<ILogger>();

        return new DosBoxLaunchStrategy(
            extractionServiceMock.Object,
            configurationMock.Object,
            messageBoxMock.Object,
            mountChdFilesMock.Object,
            mountIsoFilesMock.Object,
            discConverterMock.Object,
            debugLoggerMock.Object);
    }

    /// <summary>
    ///     Verifies that the strategy has a priority of 25.
    /// </summary>
    [Fact]
    public void PriorityIs25()
    {
        var strategy = CreateStrategy();
        Assert.Equal(25, strategy.Priority);
    }

    /// <summary>
    ///     Verifies that IsMatch returns false when the emulator name is empty.
    /// </summary>
    [Fact]
    public void IsMatchEmptyEmulatorNameReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\dos\game.zip",
            EmulatorName = ""
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false when the file path is empty.
    /// </summary>
    [Fact]
    public void IsMatchEmptyFilePathReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = "",
            EmulatorName = "DOSBox-X"
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false for a non-DOSBox emulator.
    /// </summary>
    [Fact]
    public void IsMatchNonDosBoxEmulatorReturnsFalse()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\dos\game.zip",
            EmulatorName = "Mesen"
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true for DOSBox emulator variants paired with archive files.
    /// </summary>
    /// <param name="emulatorName">The DOSBox emulator name variant to test.</param>
    [Theory]
    [InlineData("DOSBox-X")]
    [InlineData("DOSBox")]
    [InlineData("dosbox")]
    [InlineData("DOSBox Staging")]
    [InlineData("dosbox_pure")]
    public void IsMatchDosBoxEmulatorWithArchiveReturnsTrue(string emulatorName)
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\dos\game.zip",
            EmulatorName = emulatorName
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true for DOSBox with supported file formats.
    /// </summary>
    /// <param name="extension">The supported file extension to test.</param>
    [Theory]
    [InlineData(".zip")]
    [InlineData(".7z")]
    [InlineData(".rar")]
    [InlineData(".iso")]
    [InlineData(".chd")]
    public void IsMatchDosBoxWithSupportedFormatsReturnsTrue(string extension)
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = $@"C:\dos\game{extension}",
            EmulatorName = "DOSBox-X"
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns false for DOSBox with unsupported file formats.
    /// </summary>
    /// <param name="extension">The unsupported file extension to test.</param>
    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".nes")]
    [InlineData(".sfc")]
    public void IsMatchDosBoxWithUnsupportedFormatsReturnsFalse(string extension)
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = $@"C:\dos\game{extension}",
            EmulatorName = "DOSBox-X"
        };

        Assert.False(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsMatch returns true when the emulator location points to a DOSBox executable.
    /// </summary>
    [Fact]
    public void IsMatchDosBoxByEmulatorLocationReturnsTrue()
    {
        var strategy = CreateStrategy();
        var context = new LaunchContext
        {
            ResolvedFilePath = @"C:\dos\game.zip",
            EmulatorName = "SomeEmulator",
            EmulatorManager = new Emulator { EmulatorLocation = @"C:\emu\dosbox-x.exe" }
        };

        Assert.True(strategy.IsMatch(context));
    }

    /// <summary>
    ///     Verifies that IsDosBoxEmulator returns true when the emulator name is DOSBox-X.
    /// </summary>
    [Fact]
    public void IsDosBoxEmulatorWithDosBoxNameReturnsTrue()
    {
        var context = new LaunchContext { EmulatorName = "DOSBox-X" };
        Assert.True(DosBoxLaunchStrategy.IsDosBoxEmulator(context));
    }

    /// <summary>
    ///     Verifies that IsDosBoxEmulator returns true when the emulator location points to a DOSBox executable.
    /// </summary>
    [Fact]
    public void IsDosBoxEmulatorWithDosBoxPathReturnsTrue()
    {
        var context = new LaunchContext
        {
            EmulatorName = "CustomEmu",
            EmulatorManager = new Emulator { EmulatorLocation = @"C:\emu\dosbox.exe" }
        };
        Assert.True(DosBoxLaunchStrategy.IsDosBoxEmulator(context));
    }

    /// <summary>
    ///     Verifies that IsDosBoxEmulator returns false for a non-DOSBox emulator.
    /// </summary>
    [Fact]
    public void IsDosBoxEmulatorWithNonDosBoxReturnsFalse()
    {
        var context = new LaunchContext { EmulatorName = "Mesen" };
        Assert.False(DosBoxLaunchStrategy.IsDosBoxEmulator(context));
    }

    /// <summary>
    ///     Verifies that IsDosBoxEmulator detection is case-insensitive.
    /// </summary>
    [Fact]
    public void IsDosBoxEmulatorCaseInsensitive()
    {
        var context1 = new LaunchContext { EmulatorName = "dosbox" };
        var context2 = new LaunchContext { EmulatorName = "DOSBOX" };
        var context3 = new LaunchContext { EmulatorName = "DosBox" };

        Assert.True(DosBoxLaunchStrategy.IsDosBoxEmulator(context1));
        Assert.True(DosBoxLaunchStrategy.IsDosBoxEmulator(context2));
        Assert.True(DosBoxLaunchStrategy.IsDosBoxEmulator(context3));
    }

    /// <summary>
    ///     Verifies that IsDosBoxEmulator returns true for DOSBox Staging.
    /// </summary>
    [Fact]
    public void IsDosBoxEmulatorWithDosBoxStagingReturnsTrue()
    {
        var context = new LaunchContext { EmulatorName = "DOSBox Staging" };
        Assert.True(DosBoxLaunchStrategy.IsDosBoxEmulator(context));
    }

    /// <summary>
    ///     Verifies that IsDosBoxEmulator returns true for dosbox_pure.
    /// </summary>
    [Fact]
    public void IsDosBoxEmulatorWithDosBoxPureReturnsTrue()
    {
        var context = new LaunchContext { EmulatorName = "dosbox_pure" };
        Assert.True(DosBoxLaunchStrategy.IsDosBoxEmulator(context));
    }
}