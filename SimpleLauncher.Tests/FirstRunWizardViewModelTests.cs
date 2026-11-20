using SimpleLauncher.Core.Services.FirstRun;
using SimpleLauncher.Tests.TestHelpers;
using SimpleLauncher.ViewModels;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     WPF first-run wizard VM (P1-33): navigation, the Next/Finish swap and the summary text
///     that depends on whether Easy Mode actually added a system.
/// </summary>
public class FirstRunWizardViewModelTests
{
    private static FirstRunWizardViewModel CreateVm()
    {
        return new FirstRunWizardViewModel(new FirstRunWizardState(), new NoOpResourceProvider());
    }

    [Fact]
    public void NextThenAddedSystem_MovesToFinishSummary()
    {
        var vm = CreateVm();
        try
        {
            Assert.True(vm.IsWelcomeStep);
            Assert.True(vm.ShowNextButton);

            vm.NextCommand.Execute(null);

            Assert.True(vm.IsAddSystemStep);
            Assert.False(vm.CanGoNext);
            Assert.Equal(50, vm.StepProgress);
            Assert.True(vm.ShowNextButton);

            vm.State.MarkSystemAdded("Mega Drive");

            Assert.True(vm.IsFinishStep);
            Assert.True(vm.SystemAdded);
            Assert.False(vm.ShowNextButton);
            Assert.Equal(100, vm.StepProgress);
            Assert.Contains("Mega Drive", vm.FinishBody, StringComparison.Ordinal);
            Assert.Contains("Step 3 of 3", vm.StepCounterText, StringComparison.Ordinal);
        }
        finally
        {
            vm.Dispose();
        }
    }

    [Fact]
    public void SkipWithoutSystem_ShowsSkippedSummary()
    {
        var vm = CreateVm();
        try
        {
            vm.SkipCommand.Execute(null);

            Assert.True(vm.IsFinishStep);
            Assert.False(vm.SystemAdded);
            Assert.Contains("No system was added", vm.FinishBody, StringComparison.Ordinal);
        }
        finally
        {
            vm.Dispose();
        }
    }

    [Fact]
    public void ErrorBanner_TracksStateAndDismissClearsIt()
    {
        var vm = CreateVm();
        try
        {
            vm.State.ReportError("Download failed.");

            Assert.True(vm.HasError);
            Assert.Equal("Download failed.", vm.ErrorText);

            vm.DismissErrorCommand.Execute(null);

            Assert.False(vm.HasError);
            Assert.Equal("", vm.ErrorText);
        }
        finally
        {
            vm.Dispose();
        }
    }
}
