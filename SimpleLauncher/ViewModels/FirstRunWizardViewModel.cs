using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.FirstRun;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     First-run wizard (P1-33): Welcome → Add System (reusing <see cref="EasyModeView" />) → Finish.
///     Navigation, progress and the error banner come from the shared
///     <see cref="FirstRunWizardState" />; the window hosts the Easy Mode view and forwards its
///     failure/system-added events into this state.
/// </summary>
public partial class FirstRunWizardViewModel : ObservableObject, IDisposable
{
    private readonly IResourceProvider _resources;
    private bool _disposed;

    public FirstRunWizardViewModel(FirstRunWizardState state, IResourceProvider resources)
    {
        State = state;
        _resources = resources;

        State.PropertyChanged += OnStatePropertyChanged;
    }

    public FirstRunWizardState State { get; }

    /// <summary>Invoked when the wizard should close (Finish, Skip/Cancel or the window chrome).</summary>
    public Action? RequestClose { get; set; }

    public bool SystemAdded => State.SystemAdded;

    public string SystemName => State.SystemName;

    public bool IsWelcomeStep => State.IsWelcomeStep;

    public bool IsAddSystemStep => State.IsAddSystemStep;

    public bool IsFinishStep => State.IsFinishStep;

    public bool CanGoBack => State.CanGoBack;

    public bool CanGoNext => State.CanGoNext;

    public bool CanSkip => State.CanSkip;

    /// <summary>Next is replaced by Finish on the summary step.</summary>
    public bool ShowNextButton => !State.IsFinishStep;

    public double StepProgress => State.ProgressPercent;

    public string StepCounterText => string.Format(CultureInfo.CurrentCulture,
        _resources.GetString("FirstRunWizardStepCounter", "Step {0} of {1}"),
        State.StepNumber, FirstRunWizardState.StepCount);

    public string Title => L("FirstRunWizardTitle", "Welcome");

    public string WelcomeTitle => L("FirstRunWizardWelcomeTitle", "Welcome to 'Simple Launcher'!");

    public string WelcomeBody => L("FirstRunWizardWelcomeBody",
        "Welcome to 'Simple Launcher'! This short wizard helps you add your first system. You can skip it and configure everything later.");

    public string AddSystemTitle => L("FirstRunWizardAddSystemTitle", "Add your first system");

    public string AddSystemBody => L("FirstRunWizardAddSystemBody",
        "Choose a system, download its emulator and add your first system to the library.");

    public string FinishTitle => L("FirstRunWizardFinishTitle", "You're all set!");

    public string FinishBody => State.SystemAdded
        ? string.Format(CultureInfo.CurrentCulture,
            L("FirstRunWizardFinishBody",
                "Your system '{0}' is ready. Select it in the main window to start playing."),
            State.SystemName)
        : L("FirstRunWizardFinishSkippedBody",
            "No system was added. You can add one at any time from the Easy Mode menu.");

    public string NextButtonText => L("Next", "Next");

    public string FinishButtonText => L("Finish", "Finish");

    public string BackButtonText => L("Back", "Back");

    public string SkipButtonText => L("Skip", "Skip");

    public string CancelButtonText => L("Cancel", "Cancel");

    public string ErrorTitle => L("FirstRunWizardErrorTitle", "Something went wrong");

    public string DismissButtonText => L("Dismiss", "Dismiss");

    public bool HasError => State.HasError;

    public string ErrorText => State.ErrorMessage;

    [RelayCommand]
    private void Next()
    {
        State.Next();
    }

    [RelayCommand]
    private void Back()
    {
        State.Back();
    }

    [RelayCommand]
    private void Skip()
    {
        State.Skip();
    }

    [RelayCommand]
    private void Finish()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void DismissError()
    {
        State.ClearError();
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FirstRunWizardState.CurrentStep):
                OnPropertyChanged(nameof(IsWelcomeStep));
                OnPropertyChanged(nameof(IsAddSystemStep));
                OnPropertyChanged(nameof(IsFinishStep));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanSkip));
                OnPropertyChanged(nameof(ShowNextButton));
                OnPropertyChanged(nameof(StepProgress));
                OnPropertyChanged(nameof(StepCounterText));
                break;
            case nameof(FirstRunWizardState.SystemAdded):
                OnPropertyChanged(nameof(SystemAdded));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(FinishBody));
                break;
            case nameof(FirstRunWizardState.SystemName):
                OnPropertyChanged(nameof(SystemName));
                OnPropertyChanged(nameof(FinishBody));
                break;
            case nameof(FirstRunWizardState.ErrorMessage):
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ErrorText));
                break;
        }
    }

    private string L(string key, string fallback)
    {
        return _resources.GetString(key, fallback);
    }

    public void Dispose()
    {
        if (_disposed) return;

        State.PropertyChanged -= OnStatePropertyChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
