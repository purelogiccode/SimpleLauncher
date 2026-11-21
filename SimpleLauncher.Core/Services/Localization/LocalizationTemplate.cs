namespace SimpleLauncher.Core.Services.Localization;

/// <summary>
///     Replaces named placeholders in localized templates that are not intended for
///     <see cref="string.Format(string, object?)" />. The system-card help text uses
///     <c>{config.SystemName}</c>; passing it to <c>string.Format</c> throws a
///     <see cref="FormatException" /> because the placeholder is not numeric.
/// </summary>
public static class LocalizationTemplate
{
    /// <summary>The named placeholder used by the system-card help text.</summary>
    public const string SystemNamePlaceholder = "{config.SystemName}";

    /// <summary>
    ///     Replaces <see cref="SystemNamePlaceholder" /> in a localized template with the
    ///     actual system name. Numeric <c>{0}</c>-style placeholders are left untouched.
    /// </summary>
    /// <param name="template">The localized template.</param>
    /// <param name="systemName">The system name to insert.</param>
    /// <returns>The template with the named placeholder replaced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="template" /> is null.</exception>
    public static string ReplaceSystemName(string template, string? systemName)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.Replace(SystemNamePlaceholder, systemName ?? string.Empty, StringComparison.Ordinal);
    }
}
