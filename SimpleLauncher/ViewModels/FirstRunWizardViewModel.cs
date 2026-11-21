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

    /// <summary>Creates the view model and subscribes it to the shared wizard state.</summary>
    /// <param name="state">The shared navigation state.</param>
    /// <param name="resources">Localized string provider.</param>
    public FirstRunWizardViewModel(FirstRunWizardState state, IResourceProvider resources)
    {
        State = state;
        _resources = resources;

        State.PropertyChanged += OnStatePropertyChanged;
    }

    /// <summary>Gets the shared wizard navigation state.</summary>
    public FirstRunWizardState State { get; }

    /// <summary>Invoked when the wizard should close (Finish, Skip/Cancel or the window chrome).</summary>
    public Action? RequestClose { get; set; }

    /// <summary>Whether a system was added during this wizard run.</summary>
    public bool SystemAdded => State.SystemAdded;

    /// <summary>Name of the system added during this wizard run.</summary>
    public string SystemName => State.SystemName;

    /// <summary>Whether the welcome page is visible.</summary>
    public bool IsWelcomeStep => State.IsWelcomeStep;

    /// <summary>Whether the Add System page is visible.</summary>
    public bool IsAddSystemStep => State.IsAddSystemStep;

    /// <summary>Whether the summary page is visible.</summary>
    public bool IsFinishStep => State.IsFinishStep;

    /// <summary>Whether Back is enabled.</summary>
    public bool CanGoBack => State.CanGoBack;

    /// <summary>Whether Next is enabled.</summary>
    public bool CanGoNext => State.CanGoNext;

    /// <summary>Whether Skip is enabled.</summary>
    public bool CanSkip => State.CanSkip;

    /// <summary>Next is replaced by Finish on the summary step.</summary>
    public bool ShowNextButton => !State.IsFinishStep;

    /// <summary>Progress through the wizard, from 0 to 100.</summary>
    public double StepProgress => State.ProgressPercent;

    /// <summary>Localized "Step n of 3" label.</summary>
    public string StepCounterText => string.Format(CultureInfo.CurrentCulture,
        _resources.GetString("FirstRunWizardStepCounter", "Step {0} of {1}"),
        State.StepNumber, FirstRunWizardState.StepCount);

    /// <summary>Localized window title.</summary>
    public string Title => L("FirstRunWizardTitle", "Welcome");

    /// <summary>Localized welcome heading.</summary>
    public string WelcomeTitle => L("FirstRunWizardWelcomeTitle", "Welcome to 'Simple Launcher'!");

    /// <summary>Localized welcome body text.</summary>
    public string WelcomeBody => L("FirstRunWizardWelcomeBody",
        "Welcome to 'Simple Launcher'! This short wizard helps you add your first system. You can skip it and configure everything later.");

    /// <summary>Localized Add System heading.</summary>
    public string AddSystemTitle => L("FirstRunWizardAddSystemTitle", "Add your first system");

    /// <summary>Localized Add System body text.</summary>
    public string AddSystemBody => L("FirstRunWizardAddSystemBody",
        "Choose a system, download its emulator and add your first system to the library.");

    /// <summary>Localized summary heading.</summary>
    public string FinishTitle => L("FirstRunWizardFinishTitle", "You're all set!");

    /// <summary>Localized summary text, reflecting whether a system was added.</summary>
    public string FinishBody => State.SystemAdded
        ? string.Format(CultureInfo.CurrentCulture,
            L("FirstRunWizardFinishBody",
                "Your system '{0}' is ready. Select it in the main window to start playing."),
            State.SystemName)
        : L("FirstRunWizardFinishSkippedBody",
            "No system was added. You can add one at any time from the Easy Mode menu.");

    /// <summary>Localized Next button text.</summary>
    public string NextButtonText => L("Next", "Next");

    /// <summary>Localized Finish button text.</summary>
    public string FinishButtonText => L("Finish", "Finish");

    /// <summary>Localized Back button text.</summary>
    public string BackButtonText => L("Back", "Back");

    /// <summary>Localized Skip button text.</summary>
    public string SkipButtonText => L("Skip", "Skip");

    /// <summary>Localized Cancel button text.</summary>
    public string CancelButtonText => L("Cancel", "Cancel");

    /// <summary>Localized error banner title.</summary>
    public string ErrorTitle => L("FirstRunWizardErrorTitle", "Something went wrong");

    /// <summary>Localized Dismiss button text.</summary>
    public string DismissButtonText => L("Dismiss", "Dismiss");

    /// <summary>Whether the error banner is visible.</summary>
    public bool HasError => State.HasError;

    /// <summary>Text shown in the error banner.</summary>
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

    /// <summary>Unsubscribes from the shared wizard state.</summary>
    public void Dispose()
    {
        if (_disposed) return;

        State.PropertyChanged -= OnStatePropertyChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
