using System.ComponentModel;
using SimpleLauncher.Core.Services;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="CheckApplicationControlPolicyService" /> static utility methods
///     covering application control policy detection, elevation requirements, and UAC cancellation.
/// </summary>
public class CheckApplicationControlPolicyTests
{
    // IsApplicationControlPolicyBlocked tests

    /// <summary>
    ///     Verifies that an InvalidOperationException returns false for policy block detection.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithInvalidOperationExceptionReturnsFalse()
    {
        var ex = new InvalidOperationException("test");
        Assert.False(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that a Win32 error code 5 with an English policy-blocked message returns true.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32AccessDeniedAndEnglishMessageReturnsTrue()
    {
        var ex = new Win32Exception(5, "Application Control policy blocked the operation");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that a Win32 error code 5 with a Spanish policy-blocked message returns true.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32AccessDeniedAndSpanishMessageReturnsTrue()
    {
        var ex = new Win32Exception(5, "Control de aplicaciones bloqueó la ejecución");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that Win32 error code 4551 (ERROR_BLOCKED_BY_POLICY) marks the file as policy-blocked
    ///     without relying on the (localized) message (bug #67359).
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32Exception4551ReturnsTrue()
    {
        var ex = new Win32Exception(4551, "Une stratégie de contrôle d'application a bloqué ce fichier.");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that a Win32 error code 5 with a French policy-blocked message returns true.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32AccessDeniedAndFrenchMessageReturnsTrue()
    {
        var ex = new Win32Exception(5, "Une stratégie de contrôle d'application a bloqué ce fichier.");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that the app's own French wording ("politique" instead of the OS "stratégie")
    ///     also returns true for a Win32 error code 5.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32AccessDeniedAndFrenchPolitiqueMessageReturnsTrue()
    {
        var ex = new Win32Exception(5, "Une politique de contrôle d'application a bloqué ce fichier ou ce lien.");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that a Win32 error code 5 with an unrelated message returns false.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32AccessDeniedButUnrelatedMessageReturnsFalse()
    {
        var ex = new Win32Exception(5, "Access is denied");
        Assert.False(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that a Win32 error code other than 5 returns false for policy block detection.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32NonAccessDeniedCodeReturnsFalse()
    {
        var ex = new Win32Exception(2, "Application Control policy blocked");
        Assert.False(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that policy block detection is case-insensitive for English messages.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32AccessDeniedCaseInsensitiveReturnsTrue()
    {
        var ex = new Win32Exception(5, "APPLICATION CONTROL POLICY BLOCKED");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that policy block detection is case-insensitive for Spanish messages.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithWin32AccessDeniedMixedCaseSpanishReturnsTrue()
    {
        var ex = new Win32Exception(5, "CONTROL DE APLICACIONES BLOQUEÓ");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that a Win32 exception with a null message returns false for policy block detection.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithNullMessageReturnsFalse()
    {
        var ex = new Win32Exception(5);
        Assert.False(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    // IsElevationRequired tests

    /// <summary>
    ///     Verifies that Win32 error code 740 indicates elevation is required.
    /// </summary>
    [Fact]
    public void IsElevationRequiredWithWin32Exception740ReturnsTrue()
    {
        var ex = new Win32Exception(740);
        Assert.True(CheckApplicationControlPolicyService.IsElevationRequired(ex));
    }

    /// <summary>
    ///     Verifies that Win32 error codes other than 740 return false for elevation detection.
    /// </summary>
    [Fact]
    public void IsElevationRequiredWithWin32ExceptionOtherCodeReturnsFalse()
    {
        var ex = new Win32Exception(5);
        Assert.False(CheckApplicationControlPolicyService.IsElevationRequired(ex));
    }

    /// <summary>
    ///     Verifies that an InvalidOperationException returns false for elevation detection.
    /// </summary>
    [Fact]
    public void IsElevationRequiredWithInvalidOperationExceptionReturnsFalse()
    {
        var ex = new InvalidOperationException();
        Assert.False(CheckApplicationControlPolicyService.IsElevationRequired(ex));
    }

    /// <summary>
    ///     Verifies that Win32 error code 740 with a message still indicates elevation is required.
    /// </summary>
    [Fact]
    public void IsElevationRequiredWithWin32Exception740AndMessageReturnsTrue()
    {
        var ex = new Win32Exception(740, "The requested operation requires elevation");
        Assert.True(CheckApplicationControlPolicyService.IsElevationRequired(ex));
    }

    // IsOperationCanceledByUser tests

    /// <summary>
    ///     Verifies that Win32 error code 1223 indicates the operation was canceled by the user.
    /// </summary>
    [Fact]
    public void IsOperationCanceledByUserWithWin32Exception1223ReturnsTrue()
    {
        var ex = new Win32Exception(1223);
        Assert.True(CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex));
    }

    /// <summary>
    ///     Verifies that Win32 error codes other than 1223 return false for user cancellation detection.
    /// </summary>
    [Fact]
    public void IsOperationCanceledByUserWithWin32ExceptionOtherCodeReturnsFalse()
    {
        var ex = new Win32Exception(5);
        Assert.False(CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex));
    }

    /// <summary>
    ///     Verifies that an InvalidOperationException returns false for user cancellation detection.
    /// </summary>
    [Fact]
    public void IsOperationCanceledByUserWithInvalidOperationExceptionReturnsFalse()
    {
        var ex = new InvalidOperationException();
        Assert.False(CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex));
    }

    /// <summary>
    ///     Verifies that Win32 error code 1223 with a message still indicates user cancellation.
    /// </summary>
    [Fact]
    public void IsOperationCanceledByUserWithWin32Exception1223AndMessageReturnsTrue()
    {
        var ex = new Win32Exception(1223, "The operation was canceled by the user");
        Assert.True(CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex));
    }

    // IsInvalidExecutableFormat tests

    /// <summary>
    ///     Verifies that Win32 error code 193 (ERROR_BAD_EXE_FORMAT) marks the executable as invalid.
    /// </summary>
    [Fact]
    public void IsInvalidExecutableFormatWithWin32Exception193ReturnsTrue()
    {
        var ex = new Win32Exception(193);
        Assert.True(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex));
    }

    /// <summary>
    ///     Verifies that Win32 error code 216 (ERROR_EXE_MACHINE_TYPE_MISMATCH) — what modern .NET
    ///     reports for Process.Start on a non-executable file — also marks the executable as invalid
    ///     (bug #66951).
    /// </summary>
    [Fact]
    public void IsInvalidExecutableFormatWithWin32Exception216ReturnsTrue()
    {
        var ex = new Win32Exception(216);
        Assert.True(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex));
    }

    /// <summary>
    ///     Verifies that unrelated Win32 error codes do not mark the executable as invalid.
    /// </summary>
    [Fact]
    public void IsInvalidExecutableFormatWithOtherWin32CodeReturnsFalse()
    {
        var ex = new Win32Exception(5);
        Assert.False(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex));
    }

    /// <summary>
    ///     Verifies that a non-Win32 exception does not mark the executable as invalid.
    /// </summary>
    [Fact]
    public void IsInvalidExecutableFormatWithInvalidOperationExceptionReturnsFalse()
    {
        var ex = new InvalidOperationException();
        Assert.False(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex));
    }

    // Edge case tests

    /// <summary>
    ///     Verifies that an AggregateException returns false for policy block detection.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithAggregateExceptionReturnsFalse()
    {
        var ex = new AggregateException("test");
        Assert.False(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that an AggregateException returns false for elevation detection.
    /// </summary>
    [Fact]
    public void IsElevationRequiredWithAggregateExceptionReturnsFalse()
    {
        var ex = new AggregateException("test");
        Assert.False(CheckApplicationControlPolicyService.IsElevationRequired(ex));
    }

    /// <summary>
    ///     Verifies that an AggregateException returns false for user cancellation detection.
    /// </summary>
    [Fact]
    public void IsOperationCanceledByUserWithAggregateExceptionReturnsFalse()
    {
        var ex = new AggregateException("test");
        Assert.False(CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex));
    }

    /// <summary>
    ///     Verifies that all policy methods return false for a FileNotFoundException.
    /// </summary>
    [Fact]
    public void AllMethodsReturnFalseForFileNotFoundException()
    {
        var ex = new FileNotFoundException("file not found");
        Assert.False(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
        Assert.False(CheckApplicationControlPolicyService.IsElevationRequired(ex));
        Assert.False(CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex));
        Assert.False(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex));
    }

    /// <summary>
    ///     Verifies that all policy methods return false for a DirectoryNotFoundException.
    /// </summary>
    [Fact]
    public void AllMethodsReturnFalseForDirectoryNotFoundException()
    {
        var ex = new DirectoryNotFoundException("dir not found");
        Assert.False(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
        Assert.False(CheckApplicationControlPolicyService.IsElevationRequired(ex));
        Assert.False(CheckApplicationControlPolicyService.IsOperationCanceledByUser(ex));
        Assert.False(CheckApplicationControlPolicyService.IsInvalidExecutableFormat(ex));
    }

    /// <summary>
    ///     Verifies that a partial English match in the error message detects a policy block.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithPartialEnglishMatchReturnsTrue()
    {
        var ex = new Win32Exception(5, "The Application Control policy blocked this app from running");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }

    /// <summary>
    ///     Verifies that a partial Spanish match in the error message detects a policy block.
    /// </summary>
    [Fact]
    public void IsApplicationControlPolicyBlockedWithPartialSpanishMatchReturnsTrue()
    {
        var ex = new Win32Exception(5, "El Control de aplicaciones bloqueó esta aplicación");
        Assert.True(CheckApplicationControlPolicyService.IsApplicationControlPolicyBlocked(ex));
    }
}