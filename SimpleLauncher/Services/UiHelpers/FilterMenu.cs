using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Services.UiHelpers;

/// <summary>
///     Provides an alphabetical letter filter panel (A–Z, #, All) for filtering game lists, with keyboard navigation
///     support.
/// </summary>
public class FilterMenu
{
    private readonly IPlaySoundEffects _playSoundEffects;

    private Button? _selectedButton;

    /// <summary>Initializes a new instance of the FilterMenu with sound effects support.</summary>
    public FilterMenu(IPlaySoundEffects playSoundEffects)
    {
        _playSoundEffects = playSoundEffects ?? throw new ArgumentNullException(nameof(playSoundEffects));
        InitializeAllButton();
        InitializeNumberButton();
        InitializeLetterButtons();
    }

    /// <summary>Gets the StackPanel containing the filter buttons.</summary>
    public StackPanel LetterPanel { get; } = new() { Orientation = Orientation.Horizontal };

    /// <summary>Raised when a letter or filter option is selected, passing the selected letter or null for "All".</summary>
    public event EventHandler<EventArgs<string?>> OnLetterSelected = null!;

    private void InitializeNumberButton()
    {
        Button numButton = new() { Content = "#", Width = 32, Height = 32 };
        // Set AutomationProperties.Name for screen readers
        AutomationProperties.SetName(numButton,
            (string)Application.Current.TryFindResource("FilterByNumber") ?? "Filter by Number");
        numButton.Click += (_, _) =>
        {
            _playSoundEffects.PlayNotificationSound();
            UpdateSelectedButton(numButton);
            OnLetterSelected?.Invoke(this, new EventArgs<string?>("#"));
        };
        numButton.KeyDown += FilterButton_KeyDown;
        LetterPanel.Children.Add(numButton);
    }

    private void InitializeLetterButtons()
    {
        // Cache the resource lookup to avoid repeated calls
        var filterByLetterText = (string)Application.Current.TryFindResource("FilterByLetter") ?? "Filter by";

        foreach (var c in Enumerable.Range('A', 26).Select(static x => (char)x))
        {
            Button button = new() { Content = c.ToString(), Width = 32, Height = 32 };
            // Set AutomationProperties.Name for screen readers
            AutomationProperties.SetName(button, $"{filterByLetterText} {c}");
            button.Click += (_, _) =>
            {
                _playSoundEffects.PlayNotificationSound();
                UpdateSelectedButton(button);
                OnLetterSelected?.Invoke(this, new EventArgs<string?>(c.ToString()));
            };
            button.KeyDown += FilterButton_KeyDown;
            LetterPanel.Children.Add(button);
        }
    }

    private void InitializeAllButton()
    {
        const string allText = "All";
        var allButton = new Button { Content = allText, Width = 50, Height = 32 };
        // Set AutomationProperties.Name for screen readers
        AutomationProperties.SetName(allButton,
            (string)Application.Current.TryFindResource("FilterByAll") ?? "Filter by All");
        allButton.Click += (_, _) =>
        {
            _playSoundEffects.PlayNotificationSound();
            UpdateSelectedButton(allButton);
            OnLetterSelected?.Invoke(this, new EventArgs<string?>(null));
        };
        allButton.KeyDown += FilterButton_KeyDown;
        LetterPanel.Children.Add(allButton);
    }

    [SuppressMessage("ReSharper", "SwitchStatementMissingSomeEnumCasesNoDefault")]
    private void FilterButton_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not Button currentButton) return;

        var currentIndex = LetterPanel.Children.IndexOf(currentButton);
        var newIndex = -1;

        switch (e.Key)
        {
            case Key.Left:
                newIndex = currentIndex - 1;
                break;
            case Key.Right:
                newIndex = currentIndex + 1;
                break;
            case Key.Up:
                newIndex = FindNeighbor(currentButton, FocusNavigationDirection.Up);
                break;
            case Key.Down:
                newIndex = FindNeighbor(currentButton, FocusNavigationDirection.Down);
                break;
            case Key.Home:
                newIndex = 0;
                break;
            case Key.End:
                newIndex = LetterPanel.Children.Count - 1;
                break;
        }

        if (newIndex >= 0 && newIndex < LetterPanel.Children.Count)
        {
            if (LetterPanel.Children[newIndex] is Button targetButton)
            {
                targetButton.Focus(); // Move keyboard focus to the target button
                e.Handled = true; // Mark event as handled to prevent further processing
            }
        }
    }

    private int FindNeighbor(Button currentButton, FocusNavigationDirection direction)
    {
        // Get the center point of the current button in LetterPanel coordinates
        var currentCenter =
            currentButton.TranslatePoint(new Point(currentButton.ActualWidth / 2, currentButton.ActualHeight / 2),
                LetterPanel);

        Button? bestMatch = null;
        var bestDistance = double.MaxValue;

        foreach (var child in LetterPanel.Children)
        {
            if (child is not Button targetButton || targetButton == currentButton) continue;

            var targetCenter =
                targetButton.TranslatePoint(new Point(targetButton.ActualWidth / 2, targetButton.ActualHeight / 2),
                    LetterPanel);

            var isCorrectDirection = direction switch
            {
                FocusNavigationDirection.Up => targetCenter.Y < currentCenter.Y - (currentButton.ActualHeight / 2),
                FocusNavigationDirection.Down => targetCenter.Y > currentCenter.Y + (currentButton.ActualHeight / 2),
                _ => false
            };

            if (isCorrectDirection)
            {
                // Calculate distance from the current center to the target center
                var dx = targetCenter.X - currentCenter.X;
                var dy = targetCenter.Y - currentCenter.Y;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = targetButton;
                }
            }
        }

        return bestMatch != null ? LetterPanel.Children.IndexOf(bestMatch) : -1;
    }

    private void UpdateSelectedButton(Button button)
    {
        if (_selectedButton != null && _selectedButton != button)
            _selectedButton.ClearValue(Control.BackgroundProperty);

        var accentBrush = Application.Current?.Resources.Contains("MahApps.Brushes.Accent") == true
            ? Application.Current.Resources["MahApps.Brushes.Accent"] as Brush
            : null;
        if (accentBrush != null)
            button.Background = accentBrush;

        _playSoundEffects.PlayNotificationSound();
        _selectedButton = button;
    }

    /// <summary>Deselects the currently selected letter filter button.</summary>
    public void DeselectLetter()
    {
        if (_selectedButton == null) return;

        _selectedButton.ClearValue(Control.BackgroundProperty);
        _selectedButton = null;
    }
}