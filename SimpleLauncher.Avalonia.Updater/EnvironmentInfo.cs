using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SimpleLauncher.Avalonia.Updater;

/// <summary>
///     Contains detailed environment information for bug reporting
/// </summary>
internal class EnvironmentInfo
{
    /// <summary>
    ///     Gets or sets the date and time when the environment information was collected.
    /// </summary>
    public string Date { get; set; } = "";

    /// <summary>
    ///     Gets or sets the name of the application.
    /// </summary>
    public string ApplicationName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the version of the application.
    /// </summary>
    public string ApplicationVersion { get; set; } = "";

    /// <summary>
    ///     Gets or sets the operating system version string.
    /// </summary>
    public string OsVersion { get; set; } = "";

    /// <summary>
    ///     Gets or sets the processor architecture (e.g., x64, Arm64).
    /// </summary>
    public string Architecture { get; set; } = "";

    /// <summary>
    ///     Gets or sets the process bitness (32-bit or 64-bit).
    /// </summary>
    public string Bitness { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Windows version information.
    /// </summary>
    public string WindowsVersion { get; set; } = "";

    /// <summary>
    ///     Gets or sets the number of processors available on the machine.
    /// </summary>
    public string ProcessorCount { get; set; } = "";

    /// <summary>
    ///     Gets or sets the base directory of the application.
    /// </summary>
    public string BaseDirectory { get; set; } = "";

    /// <summary>
    ///     Gets or sets the path to the system's temporary folder.
    /// </summary>
    public string TempPath { get; set; } = "";

    /// <summary>
    ///     Collects all environment information
    /// </summary>
    public static EnvironmentInfo Collect()
    {
        var assembly = Assembly.GetExecutingAssembly();

        return new EnvironmentInfo
        {
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture),
            ApplicationName = assembly.GetName().Name ?? "Updater",
            ApplicationVersion = assembly.GetName().Version?.ToString() ?? "Unknown",
            OsVersion = GetOsVersion(),
            Architecture = GetArchitecture(),
            Bitness = GetBitness(),
            WindowsVersion = GetWindowsVersion(),
            ProcessorCount = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
            TempPath = Path.GetTempPath()
        };
    }

    /// <summary>
    ///     Gets the operating system version
    /// </summary>
    private static string GetOsVersion()
    {
        try
        {
            return RuntimeInformation.OSDescription;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get OS version");
            return $"Unknown (Error: {ex.Message})";
        }
    }

    /// <summary>
    ///     Gets the processor architecture
    /// </summary>
    private static string GetArchitecture()
    {
        try
        {
            return RuntimeInformation.ProcessArchitecture.ToString();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get processor architecture");
            return $"Unknown (Error: {ex.Message})";
        }
    }

    /// <summary>
    ///     Gets the bitness (32-bit or 64-bit)
    /// </summary>
    private static string GetBitness()
    {
        try
        {
            return Environment.Is64BitProcess ? "64-bit" : "32-bit";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get bitness");
            return $"Unknown (Error: {ex.Message})";
        }
    }

    /// <summary>
    ///     Gets the Windows version information using registry for accurate detection
    /// </summary>
    private static string GetWindowsVersion()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows 11 (client) still reports ProductName = "Windows 10 ..." in the
                // registry, so the build number is checked FIRST: build >= 22000 means
                // Windows 11 (or a Server 2022+ product). Checking the product name first
                // mislabeled Windows 11 bug reports as Windows 10.
                var productName = GetRegistryProductName();
                var build = Environment.OSVersion.Version.Build;
                if (build >= 22000)
                {
                    // Server product names are accurate in the registry (Server 2022/2025).
                    return !string.IsNullOrEmpty(productName) &&
                           productName.Contains("Server", StringComparison.OrdinalIgnoreCase)
                        ? $"{productName} (Build {build})"
                        : $"Windows 11 (Build {build})";
                }

                if (!string.IsNullOrEmpty(productName))
                {
                    // Windows Server 2022 check: ProductName contains "Server 2022"
                    if (productName.Contains("Server 2022", StringComparison.OrdinalIgnoreCase))
                        return $"{productName} (Build {build})";

                    // Windows 10 (build < 22000 by the check above)
                    if (productName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
                        return $"{productName} (Build {build})";
                }

                // Fallback to Environment.OSVersion with improved logic
                var osVersion = Environment.OSVersion;
                var version = osVersion.Version;

                return version.Major switch
                {
                    // Windows 10
                    10 when version.Minor == 0 => $"Windows 10 (Build {version.Build})",
                    // Windows 8.1
                    6 when version.Minor == 3 => "Windows 8.1",
                    // Windows 8
                    6 when version.Minor == 2 => "Windows 8",
                    // Windows 7
                    6 when version.Minor == 1 => "Windows 7",
                    _ => $"Windows (Version {version.Major}.{version.Minor}, Build {version.Build})"
                };
            }

            return "Non-Windows OS";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get Windows version");
            return $"Unknown (Error: {ex.Message})";
        }
    }

    /// <summary>
    ///     Gets the ProductName from Windows registry for accurate OS identification
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string? GetRegistryProductName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var productName = key.GetValue("ProductName") as string;
                return productName;
            }
        }
        catch (Exception ex)
        {
            // Silently fail and return null - we'll fall back to OS version detection
            Log.Warning(ex, "Failed to read registry for Windows version");
        }

        return null;
    }
}