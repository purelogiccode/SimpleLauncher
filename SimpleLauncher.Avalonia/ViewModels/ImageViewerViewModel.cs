using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SimpleLauncher.Core.Interfaces;
using SimpleLauncher.Core.Services;

namespace SimpleLauncher.Avalonia.ViewModels;

/// <summary>
///     ViewModel for the ImageViewerWindow.
/// </summary>
public class ImageViewerViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly IMessageBoxLibraryService _messageBox;
    private string _errorMessage = "";
    private Bitmap? _imageSource;

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
    public Bitmap? ImageSource
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
        // Explicit guard: a null path is "no image", not an exception-driven dialog (AV-01).
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            ReplaceImageSource(null);
            return;
        }

        try
        {
            // Decode straight from the file stream: no intermediate byte[] doubling memory (AV-01).
            await using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 8192, useAsync: true);
            var bitmap = Bitmap.DecodeToWidth(fs, 1200);

            ReplaceImageSource(bitmap);
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            // Notify developer
            const string contextMessage = "Failed to load the image in the Image Viewer window.";
            _logger.Error(ex, contextMessage);

            // Notify user
            await ShowLoadErrorAsync();

            ReplaceImageSource(null);
        }
    }

    /// <summary>
    ///     Loads an image from a URI (local or web).
    /// </summary>
    /// <param name="imageUri">The URI of the image.</param>
    public async Task LoadImageFromUri(Uri? imageUri)
    {
        if (imageUri is null || !UrlHelper.IsHttpUrl(imageUri.AbsoluteUri))
        {
            ReplaceImageSource(null);
            return;
        }

        try
        {
            // Download a private copy: loader-cached bitmaps are shared with other views
            // and must never be disposed by ReplaceImageSource (AV-05).
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var imageData = await httpClient.GetByteArrayAsync(imageUri);
            await using var ms = new MemoryStream(imageData);
            ReplaceImageSource(Bitmap.DecodeToWidth(ms, 1200));
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Failed to load image from URI in ImageViewerWindow: {imageUri}");
            ReplaceImageSource(null);
        }
    }

    /// <summary>
    ///     Clears the bound image, disposing the previous bitmap.
    /// </summary>
    public void ClearImage()
    {
        ReplaceImageSource(null);
    }

    /// <summary>
    ///     Swaps the bound bitmap, disposing the previous one so native memory is never
    ///     leaked across loads (AV-01).
    /// </summary>
    private void ReplaceImageSource(Bitmap? bitmap)
    {
        var old = _imageSource;
        ImageSource = bitmap;
        try
        {
            old?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed (e.g. by window teardown racing a load); nothing to do.
        }
    }

    /// <summary>
    ///     Shows the load-error dialog without ever faulting the caller's task, so
    ///     fire-and-forget callers cannot produce unobserved exceptions.
    /// </summary>
    private async Task ShowLoadErrorAsync()
    {
        try
        {
            await _messageBox.ImageViewerErrorMessageBoxAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to show the image viewer error dialog.");
        }
    }
}