using Avalonia.Controls;
using SimpleLauncher.Avalonia.ViewModels;

namespace SimpleLauncher.Avalonia;

/// <summary>
///     Window for displaying images from local paths or URIs.
/// </summary>
public partial class ImageViewerWindow : Window, IDisposable
{
    private readonly ImageViewerViewModel _viewModel;

    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageViewerWindow" /> class.
    /// </summary>
    /// <param name="viewModel">The view model providing image viewing logic.</param>
    public ImageViewerWindow(ImageViewerViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Closing += OnClosing;
    }

    /// <summary>
    ///     Clears the bound image (disposing its bitmap) so a late render can never touch
    ///     a disposed <c>Bitmap</c> still set as <c>Image.Source</c> (AV-02).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        Closing -= OnClosing;
        _viewModel.ClearImage();
        GC.SuppressFinalize(this);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        Dispose();
    }

    /// <summary>
    ///     Loads an image from a file path.
    /// </summary>
    /// <param name="imagePath">The path to the image file.</param>
    public void LoadImagePath(string? imagePath)
    {
        _ = _viewModel.LoadImageFromPathAsync(imagePath);
    }

    /// <summary>
    ///     Loads an image from a URI (local or web).
    /// </summary>
    /// <param name="imageUri">The URI of the image.</param>
    public void LoadImageUrl(Uri imageUri)
    {
        _ = _viewModel.LoadImageFromUri(imageUri);
    }
}