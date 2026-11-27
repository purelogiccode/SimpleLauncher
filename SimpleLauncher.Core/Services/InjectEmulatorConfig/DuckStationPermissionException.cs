namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Exception thrown when DuckStation configuration cannot be read or written because of file access or
///     environment issues (permissions denied, a locked or cloud-only file, an unavailable Documents folder,
///     or a stale reparse point).
/// </summary>
public class DuckStationPermissionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DuckStationPermissionException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DuckStationPermissionException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DuckStationPermissionException" /> class with a specified error message and
    ///     a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DuckStationPermissionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance with a default message.</summary>
    public DuckStationPermissionException()
    {
    }
}
