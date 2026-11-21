using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Avalonia.Services;
using SimpleLauncher.Core.Services.FirstRun;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     First-run wizard (P1-33): Welcome → Add System (the existing Easy Mode view/VM) → Finish.
///     Navigation, progress and the error banner come from the shared
///     <see cref="FirstRunWizardState" />; the Easy Mode step reuses <see cref="EasyModeViewModel" />
///     so downloads, cancel and error handling stay identical to the standalone Easy Mode window.
/// </summary>
internal partial class FirstRunWizardViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private bool _disposed;

    public FirstRunWizardViewModel(
        FirstRunWizardState state,
        EasyModeViewModel easyMode,
        LocalizationService localization)
    {
        State = state;
        EasyMode = easyMode;
        _localization = localization;

        State.PropertyChanged += OnStatePropertyChanged;
        EasyMode.PropertyChanged += OnEasyModePropertyChanged;
    }

    public FirstRunWizardState State { get; }

    public EasyModeViewModel EasyMode { get; }

    /// <summary>Invoked when the wizard should close (Finish, Skip/Cancel or the window chrome).</summary>
    public Action? RequestClose { get; set; }

    public bool SystemAdded => State.SystemAdded;

    public string SystemName => State.SystemName;

    // ── Step state ────────────────────────────────────────────────────

    public bool IsWelcomeStep => State.IsWelcomeStep;

    public bool IsAddSystemStep => State.IsAddSystemStep;

    public bool IsFinishStep => State.IsFinishStep;

    public bool CanGoBack => State.CanGoBack;

    public bool CanGoNext => State.CanGoNext;

    public bool CanSkip => State.CanSkip;

    public double StepProgress => State.ProgressPercent;

    public string StepCounterText => string.Format(CultureInfo.CurrentCulture,
        _localization.GetString("FirstRunWizardStepCounter", "Step {0} of {1}"),
        State.StepNumber, FirstRunWizardState.StepCount);

    // ── Localized text ────────────────────────────────────────────────

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

    // ── Commands ──────────────────────────────────────────────────────

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
        EasyMode.ErrorMessage = null;
    }

    // ── Change propagation ────────────────────────────────────────────

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
            case nameof(FirstRunWizardState.HasError):
                OnPropertyChanged(nameof(HasError));
                break;
        }
    }

    private void OnEasyModePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(EasyModeViewModel.ErrorMessage), StringComparison.Ordinal))
            return;

        if (EasyMode.HasError)
            State.ReportError(EasyMode.ErrorMessage);
        else
            State.ClearError();
    }

    private string L(string key, string fallback)
    {
        return _localization.GetString(key, fallback);
    }

    public void Dispose()
    {
        if (_disposed) return;

        State.PropertyChanged -= OnStatePropertyChanged;
        EasyMode.PropertyChanged -= OnEasyModePropertyChanged;
        EasyMode.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
