using System.Runtime.CompilerServices;
using SimpleLauncher.Core.Services.DebugAndBugReport;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Marks the test process (and any application process it launches) so the production
///     <see cref="BugReportApiSink" /> never submits real bug reports to the live API.
/// </summary>
internal static class BugReportIsolation
{
    [ModuleInitializer]
    internal static void DisableLiveBugReports()
    {
        Environment.SetEnvironmentVariable(BugReportApiSink.DisableBugReportsEnvironmentVariable, "1");
    }
}

public class BugReportIsolationTests
{
    /// <summary>
    ///     Verifies that the test run disables live bug reports before any test executes.
    /// </summary>
    [Fact]
    public void TestProcessDisablesLiveBugReports()
    {
        Assert.Equal("1",
            Environment.GetEnvironmentVariable(BugReportApiSink.DisableBugReportsEnvironmentVariable));
    }
}
