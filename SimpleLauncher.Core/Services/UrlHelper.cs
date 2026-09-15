using System.Diagnostics;

namespace SimpleLauncher.Core.Services;

/// <summary>
///     Validates and opens web URLs. Only absolute http/https URLs may be launched:
///     passing anything else (<c>file://</c>, <c>ms-msdt:</c>, <c>C:\evil.exe</c>, ...)
///     to <see cref="Process.Start(ProcessStartInfo)" /> with
///     <c>UseShellExecute=true</c> would execute arbitrary shell handlers (WPF-04/WPF-05).
/// </summary>
public static class UrlHelper
{
    /// <summary>
    ///     Returns true when <paramref name="url" /> is an absolute http or https URL.
    /// </summary>
    public static bool IsHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
               (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Opens an http/https URL in the default browser. Returns false — without throwing
    ///     or launching anything — when the URL is not an allowed web URL or the launch fails.
    /// </summary>
    public static bool TryOpenHttpUrlInBrowser(string? url)
    {
        if (!IsHttpUrl(url)) return false;

        try
        {
            // Dispose the returned Process to release its native handle (may be null).
            using (Process.Start(new ProcessStartInfo(url!.Trim()) { UseShellExecute = true }))
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
