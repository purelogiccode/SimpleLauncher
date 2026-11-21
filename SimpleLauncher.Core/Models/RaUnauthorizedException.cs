using System.Runtime.Serialization;

namespace SimpleLauncher.Core.Models;

/// <summary>
///     Exception thrown when the RetroAchievements API returns an Unauthorized (401) response.
/// </summary>
[Serializable]
public class RaUnauthorizedException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RaUnauthorizedException" /> class.
    /// </summary>
    public RaUnauthorizedException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RaUnauthorizedException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RaUnauthorizedException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RaUnauthorizedException" /> class with a specified error message and
    ///     inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public RaUnauthorizedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance from serialized data.</summary>
    /// <param name="info">The serialized object data.</param>
    /// <param name="context">The serialization context.</param>
    [Obsolete("This API supports obsolete formatter-based serialization.")]
    protected RaUnauthorizedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}