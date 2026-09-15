using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleLauncher.Services.Converters;

/// <summary>
///     Provides conversion of disc image files (RVZ, WBFS, GCZ, CISO, WIA) to ISO format using DolphinTool.exe.
/// </summary>
public static class ConvertDiscImageToIso
{
    private static readonly Lazy<ILogger> DebugLogger2 = new(() =>
    {
        var sp = App.ServiceProvider;
        return sp?.GetService<ILogger>() ?? Log.Logger;
    });

    private static ILogger Logger => DebugLogger2.Value;

    /// <summary>
    ///     Converts a disc image file (RVZ, WBFS, GCZ, CISO, WIA) to a temporary ISO using DolphinTool.exe.
    /// </summary>
    public static async Task<string?> ConvertToIsoAsync(string discImagePath)
    {
        try
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            var exeName = arch == Architecture.Arm64 ? "DolphinTool_arm64.exe" : "DolphinTool.exe";
            var dolphinToolPath =
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "BatchConvertToRVZ", exeName);

            if (!File.Exists(dolphinToolPath))
            {
                Logger.Debug(
                    $"[ConvertDiscImageToIso] DolphinTool not found at {dolphinToolPath}. Cannot convert disc image.");
                return null;
            }

            var dolphinDir = Path.GetDirectoryName(dolphinToolPath);

            var tempIsoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");

            // Arguments: convert --format=iso --input="in.rvz" --output="out.iso"
            var args = $"convert --format=iso --input=\"{discImagePath}\" --output=\"{tempIsoPath}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = dolphinToolPath,
                Arguments = args,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = dolphinDir
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;

            Logger.Debug($"[ConvertDiscImageToIso] Running DolphinTool with args: {args}");
            Logger.Debug($"[ConvertDiscImageToIso] Converting {Path.GetExtension(discImagePath)} to ISO.");

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
                Logger.Debug("[ConvertDiscImageToIso] Conversion timed out after 5 minutes");
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
                Logger.Debug("[ConvertDiscImageToIso] Conversion successful");
                return tempIsoPath;
            }

            Logger.Debug(
                $"[ConvertDiscImageToIso] DolphinTool failed. ExitCode: {process.ExitCode}. Error: {errorBuilder}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[ConvertDiscImageToIso] Error converting disc image to ISO");
            App.ServiceProvider.GetRequiredService<ILogger>()
                .Error(ex, "[ConvertDiscImageToIso] Error converting disc image to ISO");
            return null;
        }
    }
}