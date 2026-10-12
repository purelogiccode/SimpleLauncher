#nullable disable
using Serilog.Core;
using Serilog.Events;

namespace SimpleLauncher.SlowTests;

/// <summary>
///     A no-operation implementation of <see cref="ILogger" /> that silently discards all log events.
///     Used in tests to satisfy logger dependencies without producing output.
/// </summary>
internal sealed class NoOpLogger : ILogger
{
    /// <summary>
    ///     Discards the specified log event.
    /// </summary>
    /// <param name="logEvent">The log event to discard.</param>
    public void Write(LogEvent logEvent)
    {
    }

    /// <summary>
    ///     Returns this instance, ignoring the specified enricher.
    /// </summary>
    /// <param name="enricher">The enricher to ignore.</param>
    /// <returns>This same <see cref="ILogger" /> instance.</returns>
    public ILogger ForContext(ILogEventEnricher enricher)
    {
        return this;
    }

    /// <summary>
    ///     Returns this instance, ignoring the specified enrichers.
    /// </summary>
    /// <param name="enrichers">The enrichers to ignore.</param>
    /// <returns>This same <see cref="ILogger" /> instance.</returns>
    public ILogger ForContext(IEnumerable<ILogEventEnricher> enrichers)
    {
        return this;
    }

    /// <summary>
    ///     Returns this instance, ignoring the specified property context.
    /// </summary>
    /// <param name="propertyName">The property name to ignore.</param>
    /// <param name="value">The property value to ignore.</param>
    /// <param name="destructureObjects">Whether to destructure objects.</param>
    /// <returns>This same <see cref="ILogger" /> instance.</returns>
    public ILogger ForContext(string propertyName, object value, bool destructureObjects = false)
    {
        return this;
    }

    /// <summary>
    ///     Always returns false without parsing the message template.
    /// </summary>
    /// <param name="messageTemplate">The message template to ignore.</param>
    /// <param name="propertyValues">The property values to ignore.</param>
    /// <param name="parsedTemplate">Always set to null.</param>
    /// <param name="boundProperties">Always set to null.</param>
    /// <returns>Always returns false.</returns>
    public bool BindMessageTemplate(string messageTemplate, object[] propertyValues,
        out MessageTemplate parsedTemplate, out IEnumerable<LogEventProperty> boundProperties)
    {
        parsedTemplate = null;
        boundProperties = null;
        return false;
    }

    /// <summary>
    ///     Always returns false without binding the property.
    /// </summary>
    /// <param name="propertyName">The property name to ignore.</param>
    /// <param name="value">The property value to ignore.</param>
    /// <param name="destructureObjects">Whether to destructure objects.</param>
    /// <param name="property">Always set to null.</param>
    /// <returns>Always returns false.</returns>
    public bool BindProperty(string propertyName, object value, bool destructureObjects, out LogEventProperty property)
    {
        property = null;
        return false;
    }
}
