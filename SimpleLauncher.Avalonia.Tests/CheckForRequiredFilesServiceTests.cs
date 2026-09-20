using Moq;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="CheckForRequiredFilesService" /> — the required-files list in
///     appsettings.json uses Windows separators and must resolve on every platform.
/// </summary>
public class CheckForRequiredFilesServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sl_reqfiles_" + Guid.NewGuid().ToString("N"));

    public CheckForRequiredFilesServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task BackslashSeparators_ResolveOnEveryPlatform()
    {
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        await File.WriteAllTextAsync(Path.Combine(_root, "images", "default.png"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(_root, "mame.dat"), string.Empty);

        var messageBox = new Mock<IMessageBoxLibraryService>();
        var service = new CheckForRequiredFilesService(messageBox.Object);
        var configuration = TestEnvironment.ConfigurationFromJson(
            """{ "RequiredFiles": [ "images\\default.png", "mame.dat" ] }""");

        await service.CheckFilesAsync(configuration, TestDependencies.Logger().Object, _root);

        messageBox.Verify(m => m.HandleMissingRequiredFilesMessageBoxAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MissingFile_IsReportedWithItsResolvedPath()
    {
        string? reported = null;
        var messageBox = new Mock<IMessageBoxLibraryService>();
        messageBox.Setup(m => m.HandleMissingRequiredFilesMessageBoxAsync(It.IsAny<string>()))
            .Callback<string>(list => reported = list)
            .Returns(Task.CompletedTask);

        var service = new CheckForRequiredFilesService(messageBox.Object);
        var configuration = TestEnvironment.ConfigurationFromJson(
            """{ "RequiredFiles": [ "images\\missing.png" ] }""");

        await service.CheckFilesAsync(configuration, TestDependencies.Logger().Object, _root);

        var expected = Path.Combine(_root, "images", "missing.png");
        Assert.True(reported?.Contains(expected, StringComparison.Ordinal) == true,
            $"Expected '{expected}' in reported '{reported}'");
    }
}
