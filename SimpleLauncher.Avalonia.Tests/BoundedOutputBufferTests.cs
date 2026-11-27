using SimpleLauncher.Avalonia.Services.GameLauncher;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Regression tests for the bounded emulator output capture used by the launch pipeline
///     (bug 70321: an unbounded StringBuilder caused an OutOfMemoryException with Xenia).
/// </summary>
public class BoundedOutputBufferTests
{
    /// <summary>
    ///     Verifies that short output is preserved verbatim.
    /// </summary>
    [Fact]
    public void ShortOutputIsKeptVerbatim()
    {
        var buffer = new LauncherService.BoundedOutputBuffer();

        buffer.AppendLine("first");
        buffer.AppendLine("second");

        Assert.Equal("first" + Environment.NewLine + "second", buffer.ToString());
    }

    /// <summary>
    ///     Verifies that a chatty stream is capped while the most recent lines are retained.
    /// </summary>
    [Fact]
    public void OversizedOutputKeepsOnlyTheTail()
    {
        var buffer = new LauncherService.BoundedOutputBuffer();
        var payload = new string('x', 200);

        for (var i = 0; i < 5000; i++) buffer.AppendLine($"{i:D5}-{payload}");

        var output = buffer.ToString();
        Assert.InRange(output.Length, 1, 70_000);
        Assert.Contains("04999-", output);
        Assert.DoesNotContain("00000-", output);
    }

    /// <summary>
    ///     Verifies that a single line larger than the budget is truncated instead of retained whole.
    /// </summary>
    [Fact]
    public void SingleLineLargerThanBudgetIsTruncated()
    {
        var buffer = new LauncherService.BoundedOutputBuffer();

        buffer.AppendLine(new string('y', 200_000));

        Assert.Equal(64 * 1024, buffer.ToString().Length);
    }
}
