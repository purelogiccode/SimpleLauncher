namespace SimpleLauncher.Avalonia.Updater.Services;

/// <summary>
///     Exception thrown when an update file cannot be written or moved because another process
///     holds it open (the application not fully released yet, an antivirus scan, a second instance).
///     This is an expected user-environment condition, not a program bug: callers log it at
///     Information level so it is never submitted to the bug-report service.
/// </summary>
internal sealed class FileLockedException : IOException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileLockedException" /> class.
    /// </summary>
    public FileLockedException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileLockedException" /> class with a message.
    /// </summary>
    /// <param name="message">The message describing the locked file.</param>
    public FileLockedException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileLockedException" /> class with a message and inner
    ///     exception.
    /// </summary>
    /// <param name="message">The message describing the locked file.</param>
    /// <param name="innerException">The exception that caused the lock failure.</param>
    public FileLockedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileLockedException" /> class with a message and the
    ///     Win32/HRESULT error code of the underlying I/O failure.
    /// </summary>
    /// <param name="message">The message describing the locked file.</param>
    /// <param name="hresult">The HRESULT of the underlying I/O failure.</param>
    public FileLockedException(string message, int hresult) : base(message, hresult)
    {
    }
}
