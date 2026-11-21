namespace SimpleLauncher.Core.Services.InjectEmulatorConfig;

/// <summary>
///     Exception thrown when PCSX2 configuration cannot be read or written because of file access or
///     environment issues (permissions denied, a locked or cloud-only file, or an unavailable folder).
/// </summary>
public class Pcsx2PermissionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Pcsx2PermissionException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public Pcsx2PermissionException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Pcsx2PermissionException" /> class with a specified error message and
    ///     a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public Pcsx2PermissionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance with a default message.</summary>
    public Pcsx2PermissionException()
    {
    }
}