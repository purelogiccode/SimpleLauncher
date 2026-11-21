using System.Reflection;
using System.Runtime.CompilerServices;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Enforces the Async suffix convention for methods that return <see cref="Task" /> or
///     <see cref="ValueTask" /> in the Avalonia app and updater. Event handlers returning
///     <c>void</c> are framework callbacks and are intentionally excluded.
/// </summary>
public class AsyncNamingConventionTests
{
    /// <summary>Every Task-returning method in the Avalonia app ends with Async.</summary>
    [Fact]
    public void AvaloniaAppAsyncMethodsEndWithAsync()
    {
        AssertNoOffenders(typeof(App).Assembly);
    }

    /// <summary>
    ///     Async-void methods that are not framework event handlers must also end with Async.
    /// </summary>
    [Fact]
    public void NonEventHandlerAsyncVoidMethodsEndWithAsync()
    {
        var offenders = typeof(App).Assembly.GetTypes()
            .Where(static type => !type.Name.Contains("<>", StringComparison.Ordinal))
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(static method => method.ReturnType == typeof(void) && !method.Name.Contains("<>", StringComparison.Ordinal))
            .Where(static method => method.IsDefined(
                typeof(AsyncStateMachineAttribute), false))
            .Where(static method => !method.Name.EndsWith("Async", StringComparison.Ordinal))
            .Where(static method => !IsEventHandlerName(method.Name))
            .Select(static method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Async-void methods that are not event handlers must end with 'Async':" + Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    private static bool IsEventHandlerName(string name)
    {
        return name.Contains('_') ||
               name.StartsWith("On", StringComparison.Ordinal) ||
               name.EndsWith("Tick", StringComparison.Ordinal) ||
               name.EndsWith("Changed", StringComparison.Ordinal);
    }

    private static void AssertNoOffenders(Assembly assembly)
    {
        var offenders = assembly.GetTypes()
            .Where(static type => !type.Name.Contains("<>", StringComparison.Ordinal))
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(static method => !method.IsSpecialName && !method.Name.Contains("<>", StringComparison.Ordinal))
            .Where(static method => !method.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .Where(static method => method.ReturnType == typeof(Task) ||
                                    method.ReturnType == typeof(ValueTask) ||
                                    (method.ReturnType.IsGenericType &&
                                     (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>) ||
                                      method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>))))
            .Where(static method => !method.Name.EndsWith("Async", StringComparison.Ordinal))
            .Select(static method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Task-returning methods must end with 'Async':" + Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }
}
