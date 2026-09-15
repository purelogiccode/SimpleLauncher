using System.Net.Http;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher.ViewModels;

/// <summary>
///     ViewModel for the ImageViewerWindow.
/// </summary>
public class ImageViewerViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private string _errorMessage = "";
    private BitmapSource? _imageSource;

    /// <summary>Initializes a new instance of the <see cref="ImageViewerViewModel" />.</summary>
    /// <param name="logErrors">The logger instance.</param>
    /// <param name="messageBox">The message box service for error notifications.</param>
    public ImageViewerViewModel(ILogger logErrors, IMessageBoxLibraryService messageBox)
    {
        _logger = logErrors;
        _messageBox = messageBox;
    }

    /// <summary>
    ///     Gets or sets the image source to display.
    /// </summary>
    public BitmapSource? ImageSource
    {
        get => _imageSource;
        private set => SetProperty(ref _imageSource, value);
    }

    /// <summary>
    ///     Gets or sets an error message if image loading failed.
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    ///     Loads an image from a file path.
    /// </summary>
    /// <param name="imagePath">The path to the image file.</param>
    public async Task LoadImageFromPathAsync(string? imagePath)
    {
        // Explicit guard: a null path is "no image", not an exception-driven dialog (WPF-22).
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            ImageSource = null;
            return;
        }

        try
        {
            var imageData = await File.ReadAllBytesAsync(imagePath);
            SetImageFromBytes(imageData);
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Failed to load the image in the Image Viewer window.";
            _logger.Error(ex, contextMessage);

            await ShowLoadErrorAsync();

            ImageSource = null;
        }
    }

    /// <summary>
    ///     Loads an image from a web URI without blocking the UI thread (WPF-22).
    /// </summary>
    /// <param name="imageUri">The http/https URI of the image.</param>
    public async Task LoadImageFromUriAsync(Uri? imageUri)
    {
        if (imageUri is null || !UrlHelper.IsHttpUrl(imageUri.AbsoluteUri))
        {
            ImageSource = null;
            return;
        }

        try
        {
            // Download off the UI thread with a timeout: BitmapImage(Uri) would fetch
            // synchronously on the caller (UI) thread with no timeout.
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var imageData = await httpClient.GetByteArrayAsync(imageUri);
            SetImageFromBytes(imageData);
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Failed to load image from URI in ImageViewerWindow: {imageUri}");

            await ShowLoadErrorAsync();

            ImageSource = null;
        }
    }

    /// <summary>
    ///     Loads an image from a URI (local or web).
    /// </summary>
    /// <param name="imageUri">The URI of the image.</param>
    public void LoadImageFromUri(Uri imageUri)
    {
        // Fire-and-forget is safe: LoadImageFromUriAsync observes all exceptions internally.
        _ = LoadImageFromUriAsync(imageUri);
    }

    private void SetImageFromBytes(byte[] imageData)
    {
        using var ms = new MemoryStream(imageData);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze(); // Freeze the bitmap to make it cross-thread accessible

        ImageSource = bitmap;
    }

    /// <summary>
    ///     Shows the load-error dialog without ever faulting the caller's task, so
    ///     fire-and-forget callers cannot produce unobserved exceptions (WPF-22).
    /// </summary>
    private async Task ShowLoadErrorAsync()
    {
        try
        {
            await _messageBox.ImageViewerErrorMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to show the image viewer error dialog");
        }
    }
}