using SimpleLauncher.Core.Services.FirstRun;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Navigation contract for the shared first-run wizard state (P1-33). Both apps bind
///     their Back/Next/Skip buttons and step progress to this state machine.
/// </summary>
public class FirstRunWizardStateTests
{
    [Fact]
    public void StartsOnWelcomeStep_WithBackAndSkipDisabledAppropriately()
    {
        var state = new FirstRunWizardState();

        Assert.Equal(FirstRunWizardStep.Welcome, state.CurrentStep);
        Assert.Equal(1, state.StepNumber);
        Assert.Equal(0, state.ProgressPercent);
        Assert.True(state.IsWelcomeStep);
        Assert.False(state.CanGoBack);
        Assert.True(state.CanGoNext);
        Assert.True(state.CanSkip);
    }

    [Fact]
    public void Next_FromWelcome_OpensAddSystemStep()
    {
        var state = new FirstRunWizardState();

        Assert.True(state.Next());

        Assert.True(state.IsAddSystemStep);
        Assert.Equal(2, state.StepNumber);
        Assert.Equal(50, state.ProgressPercent);
        Assert.True(state.CanGoBack);
        Assert.True(state.CanSkip);

        // No system yet: Next stays disabled until Easy Mode adds one.
        Assert.False(state.CanGoNext);
        Assert.False(state.Next());
        Assert.True(state.IsAddSystemStep);
    }

    [Fact]
    public void MarkSystemAdded_AdvancesToSummaryWithSystemName()
    {
        var state = new FirstRunWizardState();
        state.Next();

        state.MarkSystemAdded("Super Nintendo");

        Assert.True(state.IsFinishStep);
        Assert.True(state.SystemAdded);
        Assert.Equal("Super Nintendo", state.SystemName);
        Assert.Equal(3, state.StepNumber);
        Assert.Equal(100, state.ProgressPercent);
        Assert.False(state.CanGoNext);
        Assert.False(state.CanSkip);
        Assert.True(state.CanGoBack);
    }

    [Fact]
    public void Back_FromSummary_ReturnsToAddSystemStep()
    {
        var state = new FirstRunWizardState();
        state.Next();
        state.MarkSystemAdded("NES");

        Assert.True(state.Back());

        Assert.True(state.IsAddSystemStep);
        // The successful add is remembered, so Next is enabled again.
        Assert.True(state.CanGoNext);
        Assert.True(state.Next());
        Assert.True(state.IsFinishStep);

        // Back from the first step is a no-op.
        Assert.True(state.Back());
        Assert.True(state.Back());
        Assert.True(state.IsWelcomeStep);
        Assert.False(state.Back());
    }

    [Fact]
    public void Skip_FromWelcomeAndAddSystem_GoesToSummaryWithoutSystem()
    {
        var welcome = new FirstRunWizardState();
        welcome.Skip();

        Assert.True(welcome.IsFinishStep);
        Assert.False(welcome.SystemAdded);
        Assert.False(welcome.HasError);

        // Skip does nothing on the summary page (Finish closes the wizard there).
        welcome.Skip();
        Assert.True(welcome.IsFinishStep);

        var addSystem = new FirstRunWizardState();
        addSystem.Next();
        addSystem.Skip();

        Assert.True(addSystem.IsFinishStep);
        Assert.False(addSystem.SystemAdded);
        Assert.True(addSystem.SystemName.Length == 0);
    }

    [Fact]
    public void ReportError_RaisesErrorState_AndMarkSystemAddedClearsIt()
    {
        var state = new FirstRunWizardState();
        var changed = new List<string?>();
        state.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        Assert.False(state.HasError);

        state.ReportError("Could not download the emulator.");

        Assert.True(state.HasError);
        Assert.Equal("Could not download the emulator.", state.ErrorMessage);
        Assert.Contains(nameof(FirstRunWizardState.HasError), changed, StringComparer.Ordinal);

        state.Next();
        state.MarkSystemAdded("Mega Drive");

        Assert.False(state.HasError);
        Assert.Equal("", state.ErrorMessage);
    }

    [Fact]
    public void ClearError_RemovesTheBanner()
    {
        var state = new FirstRunWizardState();
        state.ReportError("Network unavailable");

        state.ClearError();

        Assert.False(state.HasError);
        Assert.Equal("", state.ErrorMessage);
    }
}
