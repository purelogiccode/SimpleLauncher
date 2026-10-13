using Moq;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Avalonia.SlowTests;

/// <summary>
///     Common mock/builders for the slow mount-service tests. All dependencies are isolated
///     mocks (no real I/O).
/// </summary>
internal static class TestDependencies
{
    public static Mock<ILogger> Logger()
    {
        return new Mock<ILogger>();
    }

    public static Mock<IMessageBoxLibraryService> MessageBox()
    {
        return new Mock<IMessageBoxLibraryService>();
    }
}