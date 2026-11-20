using System.Runtime.InteropServices;

namespace SimpleLauncher.Core.Services.BundledTools;

/// <summary>
///     Resolves the bundled helper payloads that Simple Launcher needs at runtime. The helpers
///     ship one folder per OS/architecture under <c>tools\{tool}\{rid}\</c> (for example
///     <c>win-x64</c> or <c>linux-arm64</c>).
/// </summary>
public static class BundledToolPathResolver
{
    /// <summary>
    ///     Gets the tools\{tool}\ payload folder for the running OS/architecture (for example
    ///     win-x64 or linux-arm64), or <c>null</c> when no payload is bundled for the platform.
    /// </summary>
    public static string? GetPlatformRuntimeIdentifier()
    {
        string? operatingSystem = null;
        if (OperatingSystem.IsWindows()) operatingSystem = "win";
        else if (OperatingSystem.IsLinux()) operatingSystem = "linux";

        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        return operatingSystem == null || architecture == null ? null : $"{operatingSystem}-{architecture}";
    }

    /// <summary>
    ///     Gets a bundled tool's executable file name inside its payload folder (Windows appends
    ///     the .exe suffix; the Linux payloads are extension-less ELF binaries).
    /// </summary>
    public static string GetPlatformExecutableName(string baseName) =>
        OperatingSystem.IsWindows() ? $"{baseName}.exe" : baseName;

    /// <summary>
    ///     Gets the absolute path of a bundled helper for the running OS/architecture, or
    ///     <c>null</c> when the platform has no payload bundled.
    /// </summary>
    public static string? GetToolExecutablePath(string toolFolder, string baseName)
    {
        var runtimeIdentifier = GetPlatformRuntimeIdentifier();
        if (runtimeIdentifier == null) return null;

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", toolFolder, runtimeIdentifier,
            GetPlatformExecutableName(baseName));
    }
}
