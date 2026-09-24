using System.ComponentModel;

namespace SimpleLauncher.Core.Services;

/// <summary>
///     Provides methods to identify specific Win32 error conditions, such as application control policy blocks and
///     elevation requirements.
/// </summary>
public static class CheckApplicationControlPolicyService
{
    /// <summary>
    ///     Checks if the given exception is a Win32Exception indicating an application control policy block.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>True if the exception indicates an application control policy block, false otherwise.</returns>
    public static bool IsApplicationControlPolicyBlocked(Exception ex)
    {
        if (ex is not Win32Exception win32Ex) return false;

        // 4551 (ERROR_BLOCKED_BY_POLICY) is what Windows application control returns when a
        // policy blocks the file (observed with AppLocker/WDAC on Windows 10/11). Unlike
        // code 5 it is not overloaded, so it needs no message check and is locale-independent
        // (bug #67359: French Windows reported 4551 with "Une stratégie de contrôle
        // d'application a bloqué ce fichier.").
        if (win32Ex.NativeErrorCode == 4551) return true;

        // NativeErrorCode 5 (Access Denied) is a common manifestation of AppLocker/WDAC.
        // The message content is a more specific indicator; match the known OS wordings
        // (English, Spanish, French) case-insensitively.
        var message = win32Ex.Message;
        return win32Ex.NativeErrorCode == 5 &&
               (message.Contains("Application Control policy blocked", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Control de aplicaciones bloqueó", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("contrôle d'application a bloqué", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Checks if the given exception is a Win32Exception indicating that elevation is required.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>True if elevation is required, false otherwise.</returns>
    public static bool IsElevationRequired(Exception ex)
    {
        return ex is Win32Exception { NativeErrorCode: 740 };
    }

    /// <summary>
    ///     Checks if the given exception is a Win32Exception indicating the executable is not a valid
    ///     application for the current OS platform (e.g., a non-Win32 or wrong-architecture binary).
    ///     This is an expected user-error condition, not a bug.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>True if the executable is not a valid application for this OS platform, false otherwise.</returns>
    public static bool IsInvalidExecutableFormat(Exception ex)
    {
        if (ex is not Win32Exception win32Ex) return false;

        if (!OperatingSystem.IsWindows())
        {
            // .NET on Unix surfaces the raw errno through Win32Exception.NativeErrorCode:
            // 8 = ENOEXEC (not a valid executable for this platform), 13 = EACCES (file is
            // not marked executable / permission denied) (LB-18).
            return win32Ex.NativeErrorCode is 8 or 13;
        }

        // Win32 error code 193 (ERROR_BAD_EXE_FORMAT): "The specified executable is not a valid
        // application for this OS platform." Common when launching a non-Win32 or wrong-architecture binary.
        // 216 (ERROR_EXE_MACHINE_TYPE_MISMATCH) is what modern .NET reports for Process.Start on the
        // same condition (reproduced on Windows 10/11 with a non-PE file named .exe) — checking only
        // 193 let those launches through to the generic error path (bugs #65467 and #66951).
        return win32Ex.NativeErrorCode is 193 or 216;
    }

    /// <summary>
    ///     Checks if the given exception is a Win32Exception indicating the operation was canceled by the user.
    ///     This typically occurs when a user cancels a UAC (User Account Control) prompt.
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>True if the operation was canceled by the user, false otherwise.</returns>
    public static bool IsOperationCanceledByUser(Exception ex)
    {
        // Win32 error code 1223 (ERROR_CANCELLED) indicates the user canceled the operation.
        // This commonly happens when the user clicks "Cancel" on a UAC dialog.
        return ex is Win32Exception { NativeErrorCode: 1223 };
    }
}