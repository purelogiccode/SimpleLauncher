using Microsoft.Extensions.Configuration;
using SimpleLauncher.Core.Interfaces;
using PathHelper = SimpleLauncher.Core.Services.CheckPaths.PathHelper;

namespace SimpleLauncher.Services.WpfServices;

/// <summary>
///     WPF implementation of IImageLoader, loading images from the filesystem with fallback to a default image.
/// </summary>
public class WpfImageLoader(ILogger logErrors, IConfiguration configuration, IMessageBoxLibraryService messageBox)
    : IImageLoader
{
    private readonly string _defaultImagePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        configuration.GetValue<string>("DefaultImagePath") ?? Path.Combine("images", "default.png"));

    private readonly ILogger _logger = logErrors;
    private readonly IMessageBoxLibraryService _messageBox = messageBox;

    /// <summary>Asynchronously loads an image from the specified path, falling back to a default image on failure.</summary>
    public async Task<(Stream? image, bool isDefault)> LoadImageAsync(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return await LoadDefaultImageAsync();

        try
        {
            var imageBytes = await Task.Run(() => LoadImageBytes(imagePath));

            if (imageBytes == null) return await LoadDefaultImageAsync();

            return (new MemoryStream(imageBytes), false);
        }
        catch (NotSupportedException)
        {
            return await LoadDefaultImageAsync();
        }
        catch (Exception ex)
        {
            // Unexpected exception: potential coding bug, report it (Warning+ reaches the bug report API).
            var contextMessage = $"Failed to load primary image: {imagePath}. Attempting to load default.";
            _logger.Warning(ex, contextMessage);
            return await LoadDefaultImageAsync();
        }
    }

    /// <summary>Reads image file bytes from disk, handling long paths and access errors.</summary>
    public byte[]? LoadImageBytes(string filePath)
    {
        var longPath = PathHelper.GetLongPath(filePath);

        if (!File.Exists(longPath)) return null;

        try
        {
            return File.ReadAllBytes(longPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Expected user-file condition (locked file / insufficient permissions): not a bug.
            _logger.Information(ex,
                $"Failed to read image file '{filePath}'. It might be locked or permissions are insufficient.");
            return null;
        }
        catch (Exception ex)
        {
            // Unexpected exception: potential coding bug, report it (Warning+ reaches the bug report API).
            _logger.Warning(ex, $"An unexpected error occurred while reading image file '{filePath}'.");
            return null;
        }
    }

    private async Task<(Stream? image, bool isDefault)> LoadDefaultImageAsync()
    {
        try
        {
            var imageBytes = await Task.Run(() => LoadImageBytes(_defaultImagePath));

            if (imageBytes == null)
            {
                // Expected user-environment condition (default image missing from the app folder):
                // not a bug, keep it out of the bug report service. The user is offered a reinstall.
                const string contextMessage = "Failed to load global default image: images\\default.png.";
                _logger.Information(contextMessage);
                await _messageBox.DefaultImageNotFoundMessageBoxAsync();
                return (null, true);
            }

            return (new MemoryStream(imageBytes), true);
        }
        catch (Exception ex)
        {
            // Unexpected exception: potential coding bug, report it (Warning+ reaches the bug report API).
            const string contextMessage = "Failed to load global default image: images\\default.png.";
            _logger.Warning(ex, contextMessage);
            await _messageBox.DefaultImageNotFoundMessageBoxAsync();
            return (null, true);
        }
    }
}