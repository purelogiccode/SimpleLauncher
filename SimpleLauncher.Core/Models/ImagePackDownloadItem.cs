using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleLauncher.Core.Models;

/// <summary>
///     Represents a downloadable image pack item with state tracking for UI binding.
/// </summary>
public class ImagePackDownloadItem : INotifyPropertyChanged
{
    /// <summary>
    ///     Gets or sets the display name of the image pack.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the URL to download the image pack from.
    /// </summary>
    public string DownloadUrl { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the local path where the image pack will be extracted.
    /// </summary>
    public string ExtractPath { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the current download state of the image pack.
    /// </summary>
    public DownloadButtonState State
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(IsDownloaded));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(CanStartDownload));
        }
    }

    // Convenience properties for XAML binding
    /// <summary>
    ///     Gets a value indicating whether the download is in the idle state.
    /// </summary>
    public bool IsIdle => State == DownloadButtonState.Idle;

    /// <summary>
    ///     Gets a value indicating whether the download is currently in progress.
    /// </summary>
    public bool IsDownloading => State == DownloadButtonState.Downloading;

    /// <summary>
    ///     Gets a value indicating whether the download has completed successfully.
    /// </summary>
    public bool IsDownloaded => State == DownloadButtonState.Downloaded;

    /// <summary>
    ///     Gets a value indicating whether the download has failed.
    /// </summary>
    public bool IsFailed => State == DownloadButtonState.Failed;

    /// <summary>
    ///     Gets a value indicating whether a new download can be started (idle or failed state).
    /// </summary>
    public bool CanStartDownload => State is DownloadButtonState.Idle or DownloadButtonState.Failed;

    /// <summary>
    ///     Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged" /> for the changed property.</summary>
    /// <param name="propertyName">Name of the changed property; filled by the compiler when omitted.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}