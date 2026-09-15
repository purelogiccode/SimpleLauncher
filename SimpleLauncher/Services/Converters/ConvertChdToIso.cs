using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleLauncher.Services.Converters;

/// <summary>
///     Provides conversion of CHD disc image files to ISO format using chdman.exe.
/// </summary>
public static class ConvertChdToIso
{
    private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "SimpleLauncher");

    private static readonly Lazy<ILogger> DebugLogger2 = new(() =>
    {
        var sp = App.ServiceProvider;
        return sp?.GetService<ILogger>() ?? Log.Logger;
    });

    private static ILogger Logger => DebugLogger2.Value;

    /// <summary>
    ///     Converts a CHD file to a temporary ISO using chdman.exe.
    /// </summary>
    public static async Task<string?> ConvertChdToIsoAsync(string chdPath)
    {
        try
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            var exeName = arch == Architecture.Arm64 ? "chdman_arm64.exe" : "chdman.exe";
            var chdmanPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "BatchConvertToCHD", exeName);

            if (!File.Exists(chdmanPath))
            {
                Logger.Debug($"[ConvertChdToIso] chdman not found at {chdmanPath}. Cannot convert CHD.");
                return null;
            }

            var chdmanDir = Path.GetDirectoryName(chdmanPath);
            Directory.CreateDirectory(TempFolder);

            var tempIsoPath = Path.Combine(TempFolder, $"{Guid.NewGuid()}.iso");

            // chdman extractdvd -i "input.chd" -o "output.iso"
            var args = $"extractdvd -i \"{chdPath}\" -o \"{tempIsoPath}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = chdmanPath,
                Arguments = args,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = chdmanDir
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;

            Logger.Debug($"[ConvertChdToIso] Running chdman with args: {args}");
            Logger.Debug("[ConvertChdToIso] Converting from CHD to ISO");

            var errorBuilder = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("[ConvertChdToIso] Conversion timed out after 5 minutes");
                try
                {
                    process.Kill();
                }
                catch
                {
                    // ignored
                }

                return null;
            }

            if (process.ExitCode == 0 && File.Exists(tempIsoPath))
            {
                Logger.Debug("[ConvertChdToIso] Conversion successful");
                return tempIsoPath;
            }

            Logger.Debug($"[ConvertChdToIso] chdman failed. ExitCode: {process.ExitCode}. Error: {errorBuilder}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[ConvertChdToIso] Error converting CHD to ISO");
            App.ServiceProvider.GetRequiredService<ILogger>()
                .Error(ex, "[ConvertChdToIso] Error converting CHD to ISO");
            return null;
        }
    }
}