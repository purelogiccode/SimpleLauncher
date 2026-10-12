using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace SimpleLauncher.SlowTests.TestHelpers;

/// <summary>
///     Provides a way to mock <see cref="App.ServiceProvider" /> for unit tests.
///     The production code calls <c>App.ServiceProvider.GetRequiredService&lt;ILogger&gt;()</c>
///     on failure paths, so a mock is needed to prevent NullReferenceException in tests.
/// </summary>
public static class ServiceProviderMock
{
    private static readonly Lock Lock = new();
    private static IServiceProvider? _originalProvider;
    private static PropertyInfo? _cachedProperty;

    /// <summary>
    ///     Installs a minimal mock <see cref="IServiceProvider" /> into <see cref="App.ServiceProvider" />
    ///     that returns a no-op <see cref="ILogger" /> implementation.
    /// </summary>
    /// <param name="configuration">Optional <see cref="IConfiguration" /> to provide to services that request it.</param>
    public static void Install(IConfiguration? configuration = null)
    {
        lock (Lock)
        {
            var property = GetServiceProviderProperty();
            if (property == null)
                throw new InvalidOperationException("Could not find App.ServiceProvider property via reflection.");

            _originalProvider = property.GetValue(null) as IServiceProvider;

            var mockProvider = new MockServiceProvider(configuration);
            property.SetValue(null, mockProvider);
        }
    }

    /// <summary>
    ///     Restores the original <see cref="App.ServiceProvider" /> value.
    /// </summary>
    public static void Restore()
    {
        lock (Lock)
        {
            var property = GetServiceProviderProperty();
            if (property == null)
                throw new InvalidOperationException("Could not find App.ServiceProvider property via reflection.");

            property.SetValue(null, _originalProvider);
            _originalProvider = null;
        }
    }

    private static PropertyInfo? GetServiceProviderProperty()
    {
        if (_cachedProperty != null) return _cachedProperty;

        // Try WPF App first
        var wpfAppType = Type.GetType("SimpleLauncher.App, SimpleLauncher");
        if (wpfAppType != null)
        {
            _cachedProperty = wpfAppType.GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static);
            if (_cachedProperty != null) return _cachedProperty;
        }

        // Try Avalonia App
        var avaloniaAppType = Type.GetType("SimpleLauncher.Avalonia.App, SimpleLauncher.Avalonia");
        if (avaloniaAppType != null)
            _cachedProperty = avaloniaAppType.GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static);

        return _cachedProperty;
    }

    private sealed class MockServiceProvider : IServiceProvider
    {
        private readonly IConfiguration? _configuration;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MockServiceProvider" /> class.
        /// </summary>
        /// <param name="configuration">The configuration instance to hand out, or <c>null</c> when none is available.</param>
        public MockServiceProvider(IConfiguration? configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        ///     Resolves a service, returning a no-op logger for <see cref="ILogger" />, the supplied
        ///     configuration for <see cref="IConfiguration" />, and <c>null</c> for every other type.
        /// </summary>
        /// <param name="serviceType">The type of service being requested.</param>
        /// <returns>The resolved service instance, or <c>null</c> when the type is not supported.</returns>
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ILogger)) return new NoOpLogger();

            if (serviceType == typeof(IConfiguration)) return _configuration;

            return null;
        }
    }
}
