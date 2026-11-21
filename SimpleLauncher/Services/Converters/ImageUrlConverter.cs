using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimpleLauncher.Services.Converters;

/// <summary>
///     Converts an image URL string to a BitmapImage, with fallback to a placeholder if the URL is null or empty.
/// </summary>
public class ImageUrlConverter : IValueConverter
{
    private static readonly BitmapImage PlaceholderImage = CreatePlaceholderImage();

    /// <summary>
    ///     Converts an image URL string to a BitmapImage, falling back to a placeholder image for invalid or missing URLs.
    /// </summary>
    /// <param name="value">The image URL string to convert.</param>
    /// <param name="targetType">The target type of the conversion (unused).</param>
    /// <param name="parameter">The converter parameter (unused).</param>
    /// <param name="culture">The culture used for the conversion (unused).</param>
    /// <returns>A BitmapImage created from the URL, or a placeholder image.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string url && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                return new BitmapImage(new Uri(url, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Invalid image URL '{Url}'; using the placeholder image", url);
                return PlaceholderImage;
            }
        }

        // Return placeholder for null/empty values
        return PlaceholderImage;
    }

    /// <summary>
    ///     Converts a BitmapImage back to an image URL string.
    /// </summary>
    /// <param name="value">The value to convert (unused).</param>
    /// <param name="targetType">The target type of the conversion (unused).</param>
    /// <param name="parameter">The converter parameter (unused).</param>
    /// <param name="culture">The culture used for the conversion (unused).</param>
    /// <returns>This method is not supported and always throws.</returns>
    /// <exception cref="NotSupportedException">Thrown because the conversion is not supported.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("ConvertBack is not supported for ImageUrlConverter");
    }

    private static BitmapImage CreatePlaceholderImage()
    {
        try
        {
            // Try to load the noimage.png from resources
            return new BitmapImage(new Uri("pack://application:,,,/SimpleLauncher;component/images/noimage.png",
                UriKind.Absolute));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Bundled placeholder image is unavailable; drawing a gray placeholder");
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, 32, 32));
                drawingContext.DrawLine(new Pen(Brushes.Gray, 1), new Point(0, 0),
                    new Point(32, 32));
                drawingContext.DrawLine(new Pen(Brushes.Gray, 1), new Point(32, 0),
                    new Point(0, 32));
            }

            var renderTarget = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            renderTarget.Freeze();

            var bitmapImage = new BitmapImage();
            using (var stream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);

                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }

            return bitmapImage;
        }
    }
}