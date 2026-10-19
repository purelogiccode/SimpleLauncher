using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the SystemSelectionWindow.
/// </summary>
public partial class SystemSelectionViewModel : ObservableObject
{
    private readonly IRetroAchievementsSystemMatcher _systemMatcher;

    /// <summary>Initializes a new instance of the <see cref="SystemSelectionViewModel" /> class.</summary>
    /// <param name="systemMatcher">The RetroAchievements system matcher for retrieving supported system names.</param>
    public SystemSelectionViewModel(IRetroAchievementsSystemMatcher systemMatcher)
    {
        _systemMatcher = systemMatcher;
    }

    /// <summary>
    ///     Gets the collection of system names.
    /// </summary>
    public ObservableCollection<string> Systems { get; private set; } = [];

    /// <summary>
    ///     Gets or sets the selected system name.
    /// </summary>
    public string? SelectedSystem
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>Initializes the system list and pre-selects the system matching the current guess.</summary>
    /// <param name="currentGuess">The system name to pre-select if found in the list.</param>
    public void Initialize(string currentGuess)
    {
        var systems = _systemMatcher.GetSupportedSystemNames();
        Systems = new ObservableCollection<string>(systems);

        // Try to pre-select the guess if it exists in the list
        SelectedSystem = systems.FirstOrDefault(s => s.Equals(currentGuess, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Event raised when the window should be closed with a dialog result.
    /// </summary>
    public event EventHandler<EventArgs<bool?>>? DialogResultRequested;

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedSystem != null) DialogResultRequested?.Invoke(this, new EventArgs<bool?>(true));
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultRequested?.Invoke(this, new EventArgs<bool?>(false));
    }
}