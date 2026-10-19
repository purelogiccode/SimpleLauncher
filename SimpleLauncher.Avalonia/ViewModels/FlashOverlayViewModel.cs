using CommunityToolkit.Mvvm.ComponentModel;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the FlashOverlayWindow.
/// </summary>
public class FlashOverlayViewModel : ObservableObject
{
    /// <summary>
    ///     Gets or sets the opacity of the flash rectangle.
    /// </summary>
    public double Opacity
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    ///     Event raised when the window should be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    ///     Completes the flash animation and requests window close.
    /// </summary>
    public void OnAnimationCompleted()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}