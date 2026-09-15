using System.Security.Cryptography;
using System.Text;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.WpfServices;

/// <summary>
///     Implementation of <see cref="ICredentialProtector" /> using DPAPI (Data Protection API)
///     on Windows, with a portable fallback on non-Windows platforms (Linux).
/// </summary>
/// <remarks>
///     DPAPI is Windows-only. On Linux there is no OS key store available to this app,
///     so the portable fallback stores the data Base64-encoded (obfuscation only) and
///     logs a warning on first use — equivalent security to storing the plaintext.
/// </remarks>
public class WindowsCredentialProtector : ICredentialProtector
{
#if WINDOWS
    private static readonly byte[] Entropy = "SimpleLauncher.Salt"u8.ToArray();
#endif
    private static bool _warnedPortableFallback;

    /// <summary>Encrypts the specified plaintext using DPAPI (or the portable fallback on Linux).</summary>
    /// <param name="plaintext">The plaintext string to protect.</param>
    /// <returns>A Base64-encoded string of the protected data.</returns>
    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return "";

        if (OperatingSystem.IsWindows())
        {
#if WINDOWS
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var protectedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
#else
            // Not reachable on non-Windows builds, but keep a safe fallback for safety.
            return PortableProtect(plaintext);
#endif
        }

        return PortableProtect(plaintext);
    }

    /// <summary>Decrypts the specified protected data.</summary>
    /// <param name="protectedData">The Base64-encoded encrypted string to unprotect.</param>
    /// <returns>The decrypted plaintext, or <c>null</c> if decryption fails.</returns>
    public string? Unprotect(string protectedData)
    {
        if (string.IsNullOrEmpty(protectedData))
            return "";

        try
        {
            if (OperatingSystem.IsWindows())
            {
#if WINDOWS
                var protectedBytes = Convert.FromBase64String(protectedData);
                var plaintextBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plaintextBytes);
#else
                return PortableUnprotect(protectedData);
#endif
            }

            return PortableUnprotect(protectedData);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Data may be corrupted, truncated, or from a different user/machine.
            // FormatException covers non-Base64 garbage in settings (CORE-31).
            return null;
        }
    }

    private static string PortableProtect(string plaintext)
    {
        WarnPortableFallback();
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
    }

    private static string PortableUnprotect(string protectedData)
    {
        WarnPortableFallback();
        return Encoding.UTF8.GetString(Convert.FromBase64String(protectedData));
    }

    private static void WarnPortableFallback()
    {
        if (_warnedPortableFallback) return;

        _warnedPortableFallback = true;
        Log.Warning(
            "DPAPI is not available on this platform; RetroAchievements credentials are stored obfuscated (Base64) instead of encrypted");
    }
}