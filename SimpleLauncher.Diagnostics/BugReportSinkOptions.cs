namespace SimpleLauncher.Diagnostics;

/// <summary>
///     Configures a <see cref="BugReportApiSink" />.
/// </summary>
public sealed class BugReportSinkOptions
{
    /// <summary>
    ///     Gets the folder where the fallback log files are written.
    /// </summary>
    public required string LogFolder { get; init; }

    /// <summary>
    ///     Gets the bug-report API key. When null/blank and <see cref="UseDefaultApiKey" /> is
    ///     <see langword="true" />, the key embedded for the bundled executables is used.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the embedded API key is used when <see cref="ApiKey" />
    ///     is not supplied. Defaults to <see langword="true" /> for the standalone tools; the main
    ///     application sets it to <see langword="false" /> so a missing configured key disables
    ///     reporting instead of silently falling back.
    /// </summary>
    public bool UseDefaultApiKey { get; init; } = true;

    /// <summary>
    ///     Gets the bug-report API endpoint.
    /// </summary>
    public string ApiUrl { get; init; } = "https://www.purelogiccode.com/bugreport/api/send-bug-report";

    /// <summary>
    ///     Gets the fallback admin log file name.
    /// </summary>
    public string ErrorLogFileName { get; init; } = "error.log";

    /// <summary>
    ///     Gets the user log file name that receives submitted reports.
    /// </summary>
    public string UserLogFileName { get; init; } = "error_user.log";

    /// <summary>
    ///     Gets the critical fallback log file name.
    /// </summary>
    public string CriticalLogFileName { get; init; } = "critical_error.log";

    /// <summary>
    ///     Gets an optional factory for the HTTP client used to submit reports. When null, the
    ///     sink owns a client with a 30-second timeout.
    /// </summary>
    public Func<HttpClient>? HttpClientFactory { get; init; }

    /// <summary>
    ///     Gets an optional callback used to delete the admin log after a successful submission.
    ///     When null, the sink deletes it directly.
    /// </summary>
    public Func<string, Task>? DeleteFileAsync { get; init; }
}
