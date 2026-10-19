using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Avalonia.Interfaces;

/// <summary>
///     Resolves the display image path for system configurations.
/// </summary>
public interface ISystemImageResolverService
{
    /// <summary>
    ///     Asynchronously resolves the display image path for a system, using annotation
    ///     stripping and fuzzy matching when enabled. Always returns a usable path
    ///     (falls back to the default system image when nothing matches).
    /// </summary>
    /// <param name="config">The system configuration to resolve the image for.</param>
    /// <returns>The resolved display image path.</returns>
    Task<string> ResolveDisplayImageAsync(SystemManagerConfig config);

    /// <summary>
    ///     Resolves the best-matching system icon for the sidebar without falling back
    ///     to the default image. Returns null when no exact, annotation-stripped, or
    ///     fuzzy match exists so callers can render a glyph placeholder instead.
    /// </summary>
    /// <param name="systemName">The system name to resolve the icon for.</param>
    /// <returns>The matched image path, or null when nothing matches.</returns>
    Task<string?> ResolveSystemIconAsync(string systemName);

    /// <summary>
    ///     Synchronous variant of <see cref="ResolveSystemIconAsync" /> used by the sidebar
    ///     (the resolution is local file-system work and runs on the UI thread anyway).
    /// </summary>
    /// <param name="systemName">The system name to resolve the icon for.</param>
    /// <returns>The matched image path, or null when nothing matches.</returns>
    string? ResolveSystemIcon(string systemName);
}