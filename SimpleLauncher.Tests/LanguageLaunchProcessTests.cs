using System.Diagnostics;
using Xunit;

namespace SimpleLauncher.Tests;

public class LanguageLaunchProcessTests
{
    private const string FailedToApplyMarker = "Failed to Apply Language";
    private const string FallbackMarker = "Fallback to English language resources";
    private const string SliderNullReferenceMarker = "ButtonSizeSliderValueChanged";

    [Fact]
    public async Task Launch_AllSupportedLanguages_Succeeds()
    {
        if (Process.GetProcessesByName("SimpleLauncher").Length >
            0)
        {
            return; // an instance is already running — the single-instance mutex would interfere
        }

        var exe = LanguageLaunchTestsBase.FindAppExecutable();

        foreach (var code in LanguageLaunchTestsBase.SupportedLanguageCodes)
        {
            var failedBefore = LanguageLaunchTestsBase.CountLogMarker(FailedToApplyMarker);
            var fallbackBefore = LanguageLaunchTestsBase.CountLogMarker(FallbackMarker);

            using var process = StartProcess(exe, $"--language {code}");
            try
            {
                // Poll until the app settles: either a fallback marker appears (failure)
                // or the timeout passes with none (success). Handles async log-flush timing.
                var outcome = await WaitForOutcomeAsync(process, failedBefore, fallbackBefore, TimeSpan.FromSeconds(6));

                Assert.True(outcome == Outcome.Success,
                    $"--language {code} did not load its resources: {outcome} (see {LanguageLaunchTestsBase.LogDirectory})");
            }
            finally
            {
                KillProcess(process);
            }
        }
    }

    [Fact]
    public async Task Launch_UnsupportedLanguage_FallsBackToEnglish()
    {
        if (Process.GetProcessesByName("SimpleLauncher").Length > 0) return;

        var exe = LanguageLaunchTestsBase.FindAppExecutable();
        var failedBefore = LanguageLaunchTestsBase.CountLogMarker(FailedToApplyMarker);
        var fallbackBefore = LanguageLaunchTestsBase.CountLogMarker(FallbackMarker);

        using var process = StartProcess(exe, "--language zz");
        try
        {
            var outcome = await WaitForOutcomeAsync(process, failedBefore, fallbackBefore, TimeSpan.FromSeconds(6));

            // Unsupported codes fall back to English at Information level — the app must start
            // cleanly with no error markers (and no bug-report noise).
            Assert.True(outcome == Outcome.Success,
                $"Expected the app to start cleanly (English fallback) for unsupported code 'zz', got: {outcome}");
            Assert.Equal(failedBefore, LanguageLaunchTestsBase.CountLogMarker(FailedToApplyMarker));
            Assert.Equal(fallbackBefore, LanguageLaunchTestsBase.CountLogMarker(FallbackMarker));
        }
        finally
        {
            KillProcess(process);
        }
    }

    /// <summary>
    ///     Regression test: the card size slider's ValueChanged used to be wired in XAML,
    ///     so it fired while the loader applied Minimum/Maximum — before the MainWindow
    ///     constructor assigned _settings — and every startup logged a NullReferenceException
    ///     from ButtonSizeSliderValueChanged.
    /// </summary>
    [Fact]
    public async Task Launch_Startup_DoesNotLogSliderNullReference()
    {
        if (Process.GetProcessesByName("SimpleLauncher").Length > 0) return;

        var exe = LanguageLaunchTestsBase.FindAppExecutable();
        var nullReferencesBefore = LanguageLaunchTestsBase.CountLogMarker(SliderNullReferenceMarker);

        using var process = StartProcess(exe, "--language en");
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(7));

            Assert.False(process.HasExited, "The app exited during startup.");
            Assert.Equal(nullReferencesBefore,
                LanguageLaunchTestsBase.CountLogMarker(SliderNullReferenceMarker));
        }
        finally
        {
            KillProcess(process);
        }
    }

    /// <summary>
    ///     Waits until the app either logs a language failure (fallback markers), exits,
    ///     or has been running stably for <paramref name="settleTime" /> with no markers
    ///     (success) — whichever comes first.
    /// </summary>
    private static async Task<Outcome> WaitForOutcomeAsync(Process process, int failedBefore, int fallbackBefore,
        TimeSpan settleTime)
    {
        var started = DateTime.UtcNow;
        var deadline = started.AddSeconds(40);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited) return Outcome.ProcessExited;

            if (LanguageLaunchTestsBase.CountLogMarker(FailedToApplyMarker) > failedBefore ||
                LanguageLaunchTestsBase.CountLogMarker(FallbackMarker) > fallbackBefore)
            {
                return Outcome.FellBackToEnglish;
            }

            if (DateTime.UtcNow - started > settleTime)
                return Outcome.Success; // app stable for the settle period with no language failure

            await Task.Delay(300);
        }

        return process.HasExited ? Outcome.ProcessExited : Outcome.Success;
    }

    private static Process StartProcess(string exe, string arguments)
    {
        var psi = new ProcessStartInfo(exe, arguments)
        {
            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            UseShellExecute = false
        };
        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start the app");
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // already gone
        }
    }

    private enum Outcome
    {
        Success,
        FellBackToEnglish,
        ProcessExited
    }
}