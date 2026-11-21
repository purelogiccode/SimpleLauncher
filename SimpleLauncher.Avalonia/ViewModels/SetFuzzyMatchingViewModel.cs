using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services.SettingsManager;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the SetFuzzyMatchingWindow.
/// </summary>
internal partial class SetFuzzyMatchingViewModel : ObservableObject
{
    // Slider constraints
    /// <summary>The minimum fuzzy matching threshold allowed by the slider.</summary>
    public const double MinimumThreshold = 0.7;

    /// <summary>The maximum fuzzy matching threshold allowed by the slider.</summary>
    public const double MaximumThreshold = 0.95;

    /// <summary>The tick frequency of the slider.</summary>
    public const double TickFrequency = 0.05;

    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private readonly SettingsManagerService _settings;

    private double _thresholdValue;

    /// <summary>Initializes a new instance of the <see cref="SetFuzzyMatchingViewModel" />.</summary>
    /// <param name="settings">The settings manager service.</param>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="messageBox">The message box service.</param>
    /// <param name="resourceProvider">The resource provider for localized strings.</param>
    public SetFuzzyMatchingViewModel(SettingsManagerService settings, ILogger logErrors,
        IMessageBoxLibraryService messageBox, IResourceProvider resourceProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logErrors;
        _messageBox = messageBox;
        _ = resourceProvider;

        // Initialize values from settings
        _thresholdValue = Math.Max(MinimumThreshold, Math.Min(MaximumThreshold, _settings.FuzzyMatchingThreshold));
        CurrentThresholdText = _settings.FuzzyMatchingThreshold.ToString("P0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Gets the minimum threshold value for the slider.
    /// </summary>
    public static double Minimum => MinimumThreshold;

    /// <summary>
    ///     Gets the maximum threshold value for the slider.
    /// </summary>
    public static double Maximum => MaximumThreshold;

    /// <summary>
    ///     Gets the tick frequency for the slider.
    /// </summary>
    public static double TickFrequencyValue => TickFrequency;

    /// <summary>
    ///     Gets or sets the current threshold value from the slider.
    /// </summary>
    public double ThresholdValue
    {
        get => _thresholdValue;
        set
        {
            if (SetProperty(ref _thresholdValue, value))
            {
                // Update the percentage display when value changes
                OnPropertyChanged(nameof(ThresholdPercentage));
            }
        }
    }

    /// <summary>
    ///     Gets the threshold as a percentage string for display.
    /// </summary>
    public string ThresholdPercentage => _thresholdValue.ToString("P0", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Gets the current threshold setting as displayed text.
    /// </summary>
    public string CurrentThresholdText { get; }

    /// <summary>
    ///     Gets whether the settings can be saved.
    /// </summary>
    public bool CanSave => _settings != null;

    /// <summary>
    ///     Event raised when the window should be closed with a success result.
    /// </summary>
    public event EventHandler SaveCompleted = null!;

    /// <summary>
    ///     Event raised when the window should be closed without saving.
    /// </summary>
    public event EventHandler CancelRequested = null!;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            // Get the value directly from the slider.
            // The slider's Minimum and Maximum properties, combined with IsSnapToTickEnabled,
            // are designed to constrain the value within the desired range.
            // Math.Clamp is used here for explicit robustness against any potential
            // floating-point precision issues that might cause the slider's internal
            // value to slightly exceed its declared maximum/minimum.
            var newThreshold = Math.Clamp(ThresholdValue, MinimumThreshold, MaximumThreshold);

            _settings.FuzzyMatchingThreshold = newThreshold;
            await _settings.SaveAsync();

            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Error setting fuzzy matching threshold from slider.";
            _logger.Error(ex, contextMessage);

            // Notify the user
            await _messageBox.FuzzyMatchingErrorFailToSetThresholdMessageBoxAsync();
            // Do not close the window on error
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}