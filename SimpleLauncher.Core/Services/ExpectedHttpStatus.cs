using System.Net;

namespace SimpleLauncher.Core.Services;

/// <summary>
///     Classifies HTTP status codes that represent expected external conditions (rate limits,
///     hosting/WAF blocks) rather than application defects. Expected conditions must be logged
///     at Information level so the bug report API (Warning+) never picks them up.
/// </summary>
public static class ExpectedHttpStatus
{
    /// <summary>
    ///     HTTP 418 ("I'm a teapot"), returned by some hosting/WAF layers when a request is blocked.
    ///     The value has no <see cref="HttpStatusCode" /> member.
    /// </summary>
    public const HttpStatusCode BlockedByHosting = (HttpStatusCode)418;

    /// <summary>
    ///     Determines whether the status code is an expected external condition — a rate limit (429)
    ///     or a hosting/WAF block (418) — instead of an application defect.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the remote API.</param>
    /// <returns>True when the status represents an expected external condition.</returns>
    public static bool IsExpectedExternalCondition(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.TooManyRequests or BlockedByHosting;
    }
}
