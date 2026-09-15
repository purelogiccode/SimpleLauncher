using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleLauncher.Services.Converters;

/// <summary>
///     Provides conversion of CHD disc image files to Cue/Bin format using chdman.exe.
/// </summary>
public static class ConvertChdToCueBin
{
    private static readonly Lazy<ILogger> DebugLogger2 = new(() =>
    {
        var sp = App.ServiceProvider;
        return sp?.GetService<ILogger>() ?? Log.Logger;
    });

    private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "SimpleLauncher");

    private static ILogger Logger => DebugLogger2.Value;

    /// <summary>
    ///     Converts a CHD file to a temporary Cue/Bin using chdman.exe.
    ///     Returns the path to the generated CUE file, or null if conversion failed.
    /// </summary>
    public static async Task<string?> ConvertChdToCueBinAsync(string chdPath)
    {
        try
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            var exeName = arch == Architecture.Arm64 ? "chdman_arm64.exe" : "chdman.exe";
            var chdmanPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "BatchConvertToCHD", exeName);

            if (!File.Exists(chdmanPath))
            {
                Logger.Debug($"[ConvertChdToCueBin] chdman not found at {chdmanPath}. Cannot convert CHD.");
                return null;
            }

            var chdmanDir = Path.GetDirectoryName(chdmanPath);
            Directory.CreateDirectory(TempFolder);

            // Create a unique temp path for the .cue file
            var tempCuePath = Path.Combine(TempFolder, $"{Guid.NewGuid()}.cue");

            // chdman extractcd -i "input.chd" -o "output.cue"
            // This will also create the .bin file automatically in the same temp folder.
            var args = $"extractcd -i \"{chdPath}\" -o \"{tempCuePath}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = chdmanPath,
                Arguments = args,
                RedirectStandardOutput = false, // Not needed, prevents deadlock
                RedirectStandardError = true, // We want errors for logging
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = chdmanDir
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;

            Logger.Debug($"[ConvertChdToCueBin] Running chdman with args: {args}");
            Logger.Debug("[ConvertChdToCueBin] Converting from CHD to CUE/BIN");

            var errorBuilder = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginErrorReadLine();

            // Add 5-minute timeout to prevent hanging forever
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("[ConvertChdToCueBin] Conversion timed out after 5 minutes. Killing process");
                try
                {
                    process.Kill();
                }
                catch
                {
                    /* ignore */
                }

                return null;
            }

            if (process.ExitCode == 0 && File.Exists(tempCuePath))
            {
                Logger.Debug("[ConvertChdToCueBin] Conversion successful");
                return tempCuePath;
            }

            Logger.Debug($"[ConvertChdToCueBin] chdman failed. ExitCode: {process.ExitCode}. Error: {errorBuilder}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[ConvertChdToCueBin] Error converting CHD to CUE/BIN");
            App.ServiceProvider.GetRequiredService<ILogger>()
                .Error(ex, "[ConvertChdToCueBin] Error converting CHD to CUE/BIN");
            return null;
        }
    }
}