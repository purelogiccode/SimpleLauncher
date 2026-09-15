using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SimpleLauncher.Avalonia.Services;

/// <summary>
///     Centralized helper for opening external URLs and folders (AV-15).
///     <para>
///     Direct <c>Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })</c>
///     calls are Windows-centric (they throw on Linux for folders and happily execute
///     <c>file://</c>, <c>javascript:</c> or other non-HTTP schemes from
///     config-controlled strings). This helper validates that URLs are absolute
///     <c>http(s)</c> links and prefers Avalonia's <c>TopLevel.Launcher</c> — which
///     resolves the default browser / file manager on every OS — falling back to
///     shell-execute only when no launcher is available.
///     </para>
/// </summary>
public static class ExternalLinkHelper
{
    /// <summary>
    ///     Returns true when <paramref name="url" /> is an absolute HTTP or HTTPS URL.
    /// </summary>
    public static bool IsSafeHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
               (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Opens an HTTP(S) URL via the platform launcher, falling back to shell-execute.
    ///     Returns false when the URL fails scheme validation or cannot be opened
    ///     (callers should surface user feedback); never throws.
    /// </summary>
    /// <param name="url">The URL to open. Only absolute http/https URLs are accepted.</param>
    /// <param name="topLevel">Optional top level used for the cross-platform launcher.</param>
    public static async Task<bool> TryOpenUrlAsync(string? url, TopLevel? topLevel)
    {
        if (!IsSafeHttpUrl(url)) return false;

        var uri = new Uri(url!.Trim());
        if (topLevel?.Launcher is { } launcher)
        {
            try
            {
                await launcher.LaunchUriAsync(uri);
                return true;
            }
            catch
            {
                // Fall through to the shell-execute fallback below.
            }
        }

        return TryShellOpen(url!.Trim());
    }

    /// <summary>
    ///     Opens a folder in the OS file manager via the platform launcher, falling
    ///     back to shell-execute. Returns false when the folder does not exist or
    ///     cannot be opened (callers should surface user feedback); never throws.
    /// </summary>
    public static async Task<bool> TryOpenFolderAsync(string? folderPath, TopLevel? topLevel)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return false;

        if (topLevel?.Launcher is { } launcher)
        {
            try
            {
                await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folderPath));
                return true;
            }
            catch
            {
                // Fall through to the shell-execute fallback below.
            }
        }

        return TryShellOpen(folderPath);
    }

    private static bool TryShellOpen(string pathOrUrl)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = pathOrUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
