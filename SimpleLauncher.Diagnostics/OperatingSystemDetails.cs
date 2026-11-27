using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SimpleLauncher.Diagnostics;

/// <summary>
///     Resolves the human-readable operating-system name and version included in bug reports.
///     The lookup never throws: bug reporting must keep working when the registry,
///     <c>/etc/os-release</c> or the macOS version files are unavailable.
/// </summary>
public static class OperatingSystemDetails
{
    /// <summary>
    ///     Returns a human-readable description of the current operating system, for example
    ///     <c>Windows 11 Pro 24H2 (build 26100.2314)</c>, <c>Ubuntu 24.04.1 LTS (Linux 6.8.0)</c>
    ///     or <c>macOS 15.1 (24B83)</c>.
    /// </summary>
    /// <returns>The operating-system description, never blank.</returns>
    public static string GetDescription()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return GetWindowsDescription();
            if (OperatingSystem.IsLinux()) return GetLinuxDescription();
            if (OperatingSystem.IsMacOS()) return GetMacOsDescription();
            return RuntimeInformation.OSDescription;
        }
        catch (Exception ex)
        {
            // Debug/Information only: a warning here would be forwarded to the bug report API
            // and could recursively generate another report.
            Log.Debug(ex, "Could not resolve the detailed operating system version");
            return RuntimeInformation.OSDescription;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string GetWindowsDescription()
    {
        var fallback = $"Windows {Environment.OSVersion.Version}";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is null) return fallback;

            var productName = key.GetValue("ProductName") as string;
            var displayVersion = key.GetValue("DisplayVersion") as string;
            var build = key.GetValue("CurrentBuildNumber") as string;
            var revision = key.GetValue("UBR");

            if (string.IsNullOrWhiteSpace(productName)) return fallback;

            var result = string.IsNullOrWhiteSpace(displayVersion)
                ? productName
                : $"{productName} {displayVersion}";

            if (!string.IsNullOrWhiteSpace(build))
            {
                result += revision is int ubr
                    ? $" (build {build}.{ubr.ToString(CultureInfo.InvariantCulture)})"
                    : $" (build {build})";
            }

            return result;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException
                                       or IOException)
        {
            Log.Debug(ex, "Could not read the Windows version from the registry");
            return fallback;
        }
    }

    private static string GetLinuxDescription()
    {
        var fallback = $"Linux {Environment.OSVersion.Version}";
        try
        {
            const string osReleasePath = "/etc/os-release";
            if (!File.Exists(osReleasePath)) return fallback;

            string? prettyName = null;
            foreach (var line in File.ReadLines(osReleasePath))
            {
                if (!line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal)) continue;

                prettyName = line["PRETTY_NAME=".Length..].Trim().Trim('"');
                break;
            }

            var kernel = Environment.OSVersion.Version.ToString();
            if (string.IsNullOrWhiteSpace(prettyName)) return $"{fallback} (kernel {kernel})";
            return $"{prettyName} (Linux {kernel})";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug(ex, "Could not read /etc/os-release");
            return fallback;
        }
    }

    private static string GetMacOsDescription()
    {
        var fallback = $"macOS {Environment.OSVersion.Version}";
        try
        {
            const string plistPath = "/System/Library/CoreServices/SystemVersion.plist";
            if (!File.Exists(plistPath)) return fallback;

            var content = File.ReadAllText(plistPath);
            var productName = ReadPlistValue(content, "ProductName") ?? "macOS";
            var productVersion = ReadPlistValue(content, "ProductVersion");
            var buildVersion = ReadPlistValue(content, "ProductBuildVersion");

            if (string.IsNullOrWhiteSpace(productVersion)) return fallback;

            return string.IsNullOrWhiteSpace(buildVersion)
                ? $"{productName} {productVersion}"
                : $"{productName} {productVersion} ({buildVersion})";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug(ex, "Could not read the macOS SystemVersion.plist");
            return fallback;
        }
    }

    private static string? ReadPlistValue(string plist, string key)
    {
        var keyIndex = plist.IndexOf($"<key>{key}</key>", StringComparison.Ordinal);
        if (keyIndex < 0) return null;

        var valueStart = plist.IndexOf("<string>", keyIndex, StringComparison.Ordinal);
        if (valueStart < 0) return null;

        valueStart += "<string>".Length;
        var valueEnd = plist.IndexOf("</string>", valueStart, StringComparison.Ordinal);
        return valueEnd < 0 ? null : plist[valueStart..valueEnd].Trim();
    }
}
