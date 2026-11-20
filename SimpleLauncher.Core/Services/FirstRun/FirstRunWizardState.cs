using CommunityToolkit.Mvvm.ComponentModel;

namespace SimpleLauncher.Core.Services.FirstRun;

/// <summary>
///     Ordered steps of the first-run wizard shared by the WPF and Avalonia apps.
/// </summary>
public enum FirstRunWizardStep
{
    Welcome = 0,
    AddSystem = 1,
    Finish = 2
}

/// <summary>
///     Navigation state for the first-run wizard shown when no systems are configured.
///     Both apps share this state machine so Back/Next/Skip semantics, step progress and
///     the error banner behave identically (P1-33).
/// </summary>
public partial class FirstRunWizardState : ObservableObject
{
    /// <summary>Total number of wizard steps.</summary>
    public const int StepCount = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepNumber))]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [NotifyPropertyChangedFor(nameof(IsWelcomeStep))]
    [NotifyPropertyChangedFor(nameof(IsAddSystemStep))]
    [NotifyPropertyChangedFor(nameof(IsFinishStep))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanSkip))]
    private FirstRunWizardStep _currentStep = FirstRunWizardStep.Welcome;

    [ObservableProperty] private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private bool _systemAdded;

    [ObservableProperty] private string _systemName = "";

    /// <summary>1-based step number for the "Step n of 3" label.</summary>
    public int StepNumber => (int)CurrentStep + 1;

    /// <summary>Progress from 0 (first step) to 100 (last step).</summary>
    public double ProgressPercent => (int)CurrentStep * 100.0 / (StepCount - 1);

    public bool IsWelcomeStep => CurrentStep == FirstRunWizardStep.Welcome;

    public bool IsAddSystemStep => CurrentStep == FirstRunWizardStep.AddSystem;

    public bool IsFinishStep => CurrentStep == FirstRunWizardStep.Finish;

    /// <summary>Back is offered after the welcome page.</summary>
    public bool CanGoBack => CurrentStep != FirstRunWizardStep.Welcome;

    /// <summary>
    ///     Next is always allowed on the welcome page. The Add System page only advances
    ///     after a system was added; users who do not want one use Skip.
    /// </summary>
    public bool CanGoNext => CurrentStep switch
    {
        FirstRunWizardStep.Welcome => true,
        FirstRunWizardStep.AddSystem => SystemAdded,
        _ => false
    };

    /// <summary>Skip is offered until the summary page, where Finish replaces it.</summary>
    public bool CanSkip => CurrentStep != FirstRunWizardStep.Finish;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    /// <summary>Advances to the next step. Returns false when the move is not allowed.</summary>
    public bool Next()
    {
        if (!CanGoNext) return false;

        CurrentStep = CurrentStep switch
        {
            FirstRunWizardStep.Welcome => FirstRunWizardStep.AddSystem,
            FirstRunWizardStep.AddSystem => FirstRunWizardStep.Finish,
            _ => CurrentStep
        };

        return true;
    }

    /// <summary>Returns to the previous step. Returns false on the welcome page.</summary>
    public bool Back()
    {
        if (!CanGoBack) return false;

        CurrentStep = CurrentStep switch
        {
            FirstRunWizardStep.AddSystem => FirstRunWizardStep.Welcome,
            FirstRunWizardStep.Finish => FirstRunWizardStep.AddSystem,
            _ => CurrentStep
        };

        return true;
    }

    /// <summary>
    ///     Skips the remaining setup and goes straight to the summary page. The summary
    ///     explains that no system was added and that Easy Mode can be used later.
    /// </summary>
    public void Skip()
    {
        if (!CanSkip) return;

        ClearError();
        CurrentStep = FirstRunWizardStep.Finish;
    }

    /// <summary>Records a successful Easy Mode add and advances to the summary page.</summary>
    public void MarkSystemAdded(string? systemName)
    {
        SystemName = systemName ?? "";
        SystemAdded = true;
        ClearError();
        CurrentStep = FirstRunWizardStep.Finish;
    }

    /// <summary>Shows a message in the wizard error banner.</summary>
    public void ReportError(string? message)
    {
        ErrorMessage = message ?? "";
    }

    public void ClearError()
    {
        ErrorMessage = "";
    }
}
