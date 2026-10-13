using System.Reflection;
using Serilog.Core;
using Serilog.Events;
using SimpleLauncher.Core.Services.WpfServices;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="WindowsCredentialProtector" /> (DPAPI-based credential protection).
///     These tests require the logged-in Windows user profile and are therefore Windows-only,
///     matching the net10.0-windows target of the test project.
///     The portable-fallback level test swaps the global Serilog logger, so it shares the
///     GlobalLogMutating collection with the other log-level regression tests.
/// </summary>
[Collection(nameof(GlobalLogMutating))]
public class WindowsCredentialProtectorTests
{
    private readonly WindowsCredentialProtector _protector = new();

    [Fact]
    public void Protect_And_Unprotect_RoundTrip()
    {
        const string secret = "my-super-secret-password";

        var protectedData = _protector.Protect(secret);
        var result = _protector.Unprotect(protectedData);

        Assert.NotEqual(secret, protectedData, StringComparer.Ordinal);
        Assert.Equal(secret, result);
    }

    [Fact]
    public void Protect_SamePlaintext_ProducesDifferentCiphertexts()
    {
        // DPAPI uses randomized encryption, so two encryptions of the same value must differ
        var first = _protector.Protect("same-value");
        var second = _protector.Protect("same-value");

        Assert.NotEqual(first, second, StringComparer.Ordinal);
        Assert.Equal("same-value", _protector.Unprotect(first), StringComparer.Ordinal);
        Assert.Equal("same-value", _protector.Unprotect(second), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Protect_NullOrEmpty_ReturnsEmptyString(string? plaintext)
    {
        Assert.Equal("", _protector.Protect(plaintext!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Unprotect_NullOrEmpty_ReturnsEmptyString(string? protectedData)
    {
        Assert.Equal("", _protector.Unprotect(protectedData!));
    }

    [Fact]
    public void Unprotect_ValidBase64ButInvalidCiphertext_ReturnsNull()
    {
        // Valid Base64 that is not DPAPI-encrypted data must not throw and must return null
        var garbage = Convert.ToBase64String("not encrypted data"u8.ToArray());
        Assert.Null(_protector.Unprotect(garbage));
    }

    [Fact]
    public void Unprotect_UnicodeRoundTrip()
    {
        const string unicodeSecret = "pässwörd-🔐-ñandú";

        var protectedData = _protector.Protect(unicodeSecret);
        Assert.Equal(unicodeSecret, _protector.Unprotect(protectedData), StringComparer.Ordinal);
    }

    /// <summary>
    ///     Bug 67776: the portable-fallback notice (DPAPI unavailable on this platform)
    ///     must log at Information — never Warning/Error, which reach the bug-report
    ///     sink. Pins the fix from 35f0c8b0 and the corrected class remarks.
    /// </summary>
    [Fact]
    public void WarnPortableFallback_LogsInformationNeverWarningOrError()
    {
        var sink = new CaptureSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        var flag = typeof(WindowsCredentialProtector).GetField("_warnedPortableFallback",
            BindingFlags.NonPublic | BindingFlags.Static);
        var warnMethod = typeof(WindowsCredentialProtector).GetMethod("WarnPortableFallback",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(flag);
        Assert.NotNull(warnMethod);

        var previousFlag = flag.GetValue(null);
        try
        {
            // The method logs only on first use — reset so the test is deterministic.
            flag.SetValue(null, false);
            warnMethod.Invoke(null, null);

            Assert.DoesNotContain(sink.Events, static e => e.Level >= LogEventLevel.Warning);
            Assert.Contains(sink.Events, static e =>
                e.Level == LogEventLevel.Information &&
                e.RenderMessage().Contains("DPAPI is not available on this platform", StringComparison.Ordinal));
        }
        finally
        {
            flag.SetValue(null, previousFlag);
            Log.Logger = previous;
        }
    }

    /// <summary>Serilog sink that records every emitted event for level assertions.</summary>
    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            lock (Events)
            {
                Events.Add(logEvent);
            }
        }
    }
}