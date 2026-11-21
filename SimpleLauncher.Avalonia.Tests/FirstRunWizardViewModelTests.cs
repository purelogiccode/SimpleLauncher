using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Avalonia.ViewModels;
using SimpleLauncher.Core.Services.FirstRun;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Avalonia first-run wizard VM (P1-33): navigation mirrors the shared state, Easy Mode
///     failures surface in the banner, and the summary reflects whether a system was added.
/// </summary>
public class FirstRunWizardViewModelTests
{
    private static FirstRunWizardViewModel CreateVm()
    {
        return new FirstRunWizardViewModel(
            new FirstRunWizardState(),
            EasyModeViewModelTests.CreateVm(),
            new LocalizationService());
    }

    [Fact]
    public void NextThenAddedSystem_MovesToFinishSummary()
    {
        var vm = CreateVm();
        try
        {
            Assert.True(vm.IsWelcomeStep);
            Assert.True(vm.CanGoNext);

            vm.NextCommand.Execute(null);

            Assert.True(vm.IsAddSystemStep);
            Assert.False(vm.CanGoNext);
            Assert.True(vm.CanGoBack);
            Assert.Equal(50, vm.StepProgress);

            vm.State.MarkSystemAdded("Super Nintendo");

            Assert.True(vm.IsFinishStep);
            Assert.True(vm.SystemAdded);
            Assert.Equal(100, vm.StepProgress);
            Assert.Contains("Super Nintendo", vm.FinishBody, StringComparison.Ordinal);
            Assert.False(vm.CanSkip);
        }
        finally
        {
            vm.Dispose();
        }
    }

    [Fact]
    public void EasyModeFailure_ShowsInBanner_AndDismissClearsIt()
    {
        var vm = CreateVm();
        try
        {
            Assert.False(vm.HasError);

            vm.EasyMode.ErrorMessage = "Could not download the emulator.";

            Assert.True(vm.HasError);
            Assert.Equal("Could not download the emulator.", vm.ErrorText);

            vm.DismissErrorCommand.Execute(null);

            Assert.False(vm.HasError);
            Assert.Null(vm.EasyMode.ErrorMessage);
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
}
