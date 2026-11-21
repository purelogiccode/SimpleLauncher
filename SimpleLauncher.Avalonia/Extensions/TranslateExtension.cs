using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SimpleLauncher.Avalonia.Services;

namespace SimpleLauncher.Avalonia.Extensions;

/// <summary>
///     Resolves a localized string from the <see cref="Services.LocalizationService" /> at XAML
///     parse time (the Avalonia app requires a restart after a language change, so parse-time
///     resolution matches the runtime behavior of every other localized string).
///     Falls back to the key itself when the service or the key is unavailable.
/// </summary>
internal class TranslateExtension : MarkupExtension
{
    // Single constructor: the designer's runtime XAML compiler resolves the
    // positional {ext:Translate Key} argument against the ctor list and fails
    // with "Parameters mismatch" when a parameterless overload also exists.
    public TranslateExtension(string key)
    {
        Key = key;
    }

    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key)) return string.Empty;

        var localization = App.ServiceProvider?.GetService<LocalizationService>();
        return localization?.GetString(Key) ?? Key;
    }
}