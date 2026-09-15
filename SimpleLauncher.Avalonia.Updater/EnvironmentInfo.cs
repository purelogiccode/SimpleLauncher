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
                // Try to get accurate Windows version from registry first
                var productName = GetRegistryProductName();
                if (!string.IsNullOrEmpty(productName))
                {
                    // Windows 11 check: ProductName contains "Windows 11"
                    if (productName.Contains("Windows 11", StringComparison.OrdinalIgnoreCase))
                        return $"{productName} (Build {Environment.OSVersion.Version.Build})";

                    // Windows Server 2022 check: ProductName contains "Server 2022"
                    if (productName.Contains("Server 2022", StringComparison.OrdinalIgnoreCase))
                        return $"{productName} (Build {Environment.OSVersion.Version.Build})";

                    // Windows 10 check
                    if (productName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
                        return $"{productName} (Build {Environment.OSVersion.Version.Build})";
                }

                // Fallback to Environment.OSVersion with improved logic
                var osVersion = Environment.OSVersion;
                var version = osVersion.Version;

                // Windows 11 and Server 2022 both have build >= 22000
                // Use registry to differentiate, or fall back to Major.Minor check
                if (version is { Build: >= 22000, Major: 10 })
                {
                    // Without registry, we can't be 100% sure, but we'll assume Windows 11 for client OS
                    // and note the uncertainty
                    return $"Windows 11 or Server 2022 (Build {version.Build})";
                }

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