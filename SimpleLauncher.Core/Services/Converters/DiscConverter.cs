using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CHDSharp;
using CHDSharp.Models;
using PBPSharp;
using PBPSharp.Models;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.Converters;

/// <summary>
///     Converts disc image formats (CHD, PBP, and other disc images) to ISO or CUE/BIN. CHD
///     conversion uses the managed CHDSharp decoder on every platform (Linux/macOS have no
///     chdman), with the bundled chdman kept as a Windows fallback; PBP conversion uses the
///     managed PBPSharp library and DolphinTool handles the remaining disc images.
/// </summary>
public class DiscConverter : IDiscConverter
{
    private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "SimpleLauncher");

    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiscConverter" /> class.
    /// </summary>
    /// <param name="logger">The logger used to record conversion activity.</param>
    public DiscConverter(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Converts a CHD disc image to an ISO file (managed CHDSharp first, bundled chdman as
    ///     the Windows fallback).
    /// </summary>
    /// <param name="chdPath">The path of the CHD file to convert.</param>
    /// <returns>The path of the converted ISO file, or null if the conversion failed.</returns>
    public async Task<string?> ConvertChdToIsoAsync(string chdPath)
    {
        var managedPath = await Task.Run(() => TryExtractWithChdSharp(chdPath, ".iso", "[ConvertChdToIso]"));
        if (managedPath != null) return managedPath;

        return await RunChdmanAsync(chdPath, "extractdvd", ".iso", "[ConvertChdToIso]");
    }

    /// <summary>
    ///     Converts a CHD disc image to a CUE/BIN pair (managed CHDSharp first, bundled chdman
    ///     as the Windows fallback).
    /// </summary>
    /// <param name="chdPath">The path of the CHD file to convert.</param>
    /// <returns>The path of the converted CUE file, or null if the conversion failed.</returns>
    public async Task<string?> ConvertChdToCueBinAsync(string chdPath)
    {
        var managedPath = await Task.Run(() => TryExtractWithChdSharp(chdPath, ".cue", "[ConvertChdToCueBin]"));
        if (managedPath != null) return managedPath;

        return await RunChdmanAsync(chdPath, "extractcd", ".cue", "[ConvertChdToCueBin]");
    }

    /// <summary>
    ///     Extracts a CHD with the managed CHDSharp decoder into the conversion temp folder:
    ///     CD images become a CUE/BIN pair and DVD images become an ISO. Returns the path of
    ///     the requested file, or null when the CHD is not the expected media type, cannot be
    ///     decoded, or the extraction fails (callers then try the Windows chdman fallback).
    /// </summary>
    private string? TryExtractWithChdSharp(string chdPath, string expectedExtension, string logTag)
    {
        Directory.CreateDirectory(TempFolder);
        var baseName = Guid.NewGuid().ToString("N");

        try
        {
            var openError = ChdFile.Open(chdPath, out var chd);
            if (openError != ChdError.Chderrnone || chd is null)
            {
                _logger.Debug($"{logTag} CHDSharp could not open '{chdPath}': {openError}");
                return null;
            }

            using (chd)
            {
                var expectsCue = expectedExtension.Equals(".cue", StringComparison.OrdinalIgnoreCase);
                if (expectsCue ? !chd.IsCd : !chd.IsDvd)
                {
                    _logger.Debug(
                        $"{logTag} CHDSharp: '{chdPath}' is not a {(expectsCue ? "CD" : "DVD")} image; skipping managed extraction.");
                    return null;
                }

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var created = chd.ExtractToDirectory(TempFolder, baseName, null, timeoutCts.Token, cooked: true);

                var convertedPath = created.FirstOrDefault(file =>
                    Path.GetExtension(file).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase));
                if (convertedPath != null)
                {
                    _logger.Debug($"{logTag} Conversion successful (CHDSharp)");
                    return convertedPath;
                }

                _logger.Debug($"{logTag} CHDSharp produced no '{expectedExtension}' file for '{chdPath}'.");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug($"{logTag} CHDSharp extraction timed out after 5 minutes");
        }
        catch (InvalidDataException ex)
        {
            // Expected user-data condition (corrupt or unsupported CHD): Information level.
            _logger.Information($"{logTag} CHDSharp extraction failed for '{chdPath}': {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, $"{logTag} CHDSharp extraction failed for '{chdPath}'");
        }

        CleanupTempBaseName(baseName);
        return null;
    }

    /// <summary>
    ///     Deletes the files a failed CHDSharp extraction may have left in the temp folder.
    /// </summary>
    private static void CleanupTempBaseName(string baseName)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(TempFolder, baseName + ".*"))
                TryDeleteFile(file);
        }
        catch
        {
            // Best effort only; the OS will eventually clean up temp files.
        }
    }

    /// <summary>
    ///     Windows fallback: runs a chdman extraction command with the bundled chdman. Returns
    ///     null on non-Windows platforms and when chdman is not shipped.
    /// </summary>
    private async Task<string?> RunChdmanAsync(string chdPath, string command, string outputExtension, string logTag)
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            var exeName = arch == Architecture.Arm64 ? "chdman_arm64.exe" : "chdman.exe";
            var chdmanPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "BatchConvertToCHD", exeName);

            if (!File.Exists(chdmanPath))
            {
                _logger.Debug($"{logTag} chdman not found at {chdmanPath}. Cannot convert CHD.");
                return null;
            }

            Directory.CreateDirectory(TempFolder);
            var tempOutputPath = Path.Combine(TempFolder, $"{Guid.NewGuid()}{outputExtension}");
            var args = $"{command} -i \"{chdPath}\" -o \"{tempOutputPath}\"";

            if (await TryRunChdmanAsync(chdmanPath, args, tempOutputPath, logTag))
            {
                _logger.Debug($"{logTag} Conversion successful (chdman)");
                return tempOutputPath;
            }

            TryDeleteFile(tempOutputPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"{logTag} Error converting CHD");
            return null;
        }
    }

    /// <summary>
    ///     Runs the bundled chdman and reports whether it produced the expected output file.
    /// </summary>
    private async Task<bool> TryRunChdmanAsync(string chdmanPath, string args, string expectedOutputPath, string logTag)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = chdmanPath,
            Arguments = args,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var chdmanDir = Path.GetDirectoryName(chdmanPath);
        if (!string.IsNullOrEmpty(chdmanDir)) processStartInfo.WorkingDirectory = chdmanDir;

        using var process = new Process();
        process.StartInfo = processStartInfo;

        _logger.Debug($"{logTag} Running chdman with args: {args}");

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
            _logger.Debug($"{logTag} Conversion timed out after 5 minutes");
            try
            {
                process.Kill();
            }
            catch
            {
                /* ignored */
            }

            return false;
        }

        if (process.ExitCode == 0 && File.Exists(expectedOutputPath))
        {
            return true;
        }

        _logger.Debug($"{logTag} chdman failed. ExitCode: {process.ExitCode}. Error: {errorBuilder}");
        return false;
    }

    /// <summary>
    ///     Best-effort deletion of a single temporary conversion file.
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort only; the OS will eventually clean up temp files.
        }
    }

    /// <summary>
    ///     Converts a PBP disc image to a CUE/BIN pair using the managed PBPSharp library.
    /// </summary>
    /// <param name="pbpPath">The path of the PBP file to convert.</param>
    /// <returns>The path of the converted CUE file, or null if the conversion failed.</returns>
    public async Task<string?> ConvertPbpToCueBinAsync(string pbpPath)
    {
        // Declared outside the try so the catch can clean up partial files left by exceptions.
        string? tempCuePath = null;
        string? tempBinPath = null;

        try
        {
            Directory.CreateDirectory(TempFolder);

            var tempFileName = Guid.NewGuid().ToString();
            tempCuePath = Path.Combine(TempFolder, $"{tempFileName}.cue");
            tempBinPath = Path.Combine(TempFolder, $"{tempFileName}.bin");

            _logger.Debug("[ConvertPbpToCueBin] Converting from PBP to CUE/BIN using PBPSharp");

            return await Task.Run(() =>
            {
                var openError = PbpFile.Open(pbpPath, out var pbp);
                if (openError != PbpError.None || pbp is null)
                {
                    _logger.Debug($"[ConvertPbpToCueBin] Failed to open PBP file '{pbpPath}': {openError}.");
                    return null;
                }

                using (pbp)
                {
                    if (pbp.Discs.Count == 0)
                    {
                        _logger.Debug("[ConvertPbpToCueBin] PBP contains no discs");
                        return null;
                    }

                    // Extract only disc 1 (Discs[0]) since we can only play one disc at a time.
                    var disc = pbp.Discs[0];
                    var extractError = disc.ExtractToBinCue(tempBinPath, tempCuePath);
                    if (extractError != PbpError.None)
                    {
                        _logger.Debug($"[ConvertPbpToCueBin] PBPSharp extraction failed: {extractError}.");
                        TryDeleteTempFiles(tempCuePath, tempBinPath);
                        return null;
                    }
                }

                if (File.Exists(tempCuePath) && File.Exists(tempBinPath))
                {
                    _logger.Debug("[ConvertPbpToCueBin] Conversion successful");
                    return tempCuePath;
                }

                _logger.Debug("[ConvertPbpToCueBin] Conversion failed: output files were not created");
                TryDeleteTempFiles(tempCuePath, tempBinPath);
                return null;
            });
        }
        catch (Exception ex)
        {
            // Expected user-data condition (corrupt or unsupported PBP file): Information
            // level so it is never reported as a bug.
            _logger.Information(ex, "[ConvertPbpToCueBin] Error converting PBP to CUE/BIN");
            // An exception can leave partial .bin/.cue files behind; delete them here because
            // the non-throwing failure paths are already handled inside the conversion lambda.
            TryDeleteTempFiles(tempCuePath!, tempBinPath!);
            return null;
        }
    }

    /// <summary>
    ///     Converts a disc image file (such as RVZ) to an ISO file using DolphinTool.
    /// </summary>
    /// <param name="discImagePath">The path of the disc image file to convert.</param>
    /// <returns>The path of the converted ISO file, or null if the conversion failed.</returns>
    public async Task<string?> ConvertToIsoAsync(string discImagePath)
    {
        try
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            var exeName = arch == Architecture.Arm64 ? "DolphinTool_arm64.exe" : "DolphinTool.exe";
            var dolphinToolPath =
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "BatchConvertToRVZ", exeName);

            if (!File.Exists(dolphinToolPath))
            {
                _logger.Debug(
                    $"[ConvertDiscImageToIso] DolphinTool not found at {dolphinToolPath}. Cannot convert disc image.");
                return null;
            }

            var dolphinDir = Path.GetDirectoryName(dolphinToolPath);

            var tempIsoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");

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

            _logger.Debug($"[ConvertDiscImageToIso] Running DolphinTool with args: {args}");
            _logger.Debug($"[ConvertDiscImageToIso] Converting {Path.GetExtension(discImagePath)} to ISO.");

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
                _logger.Debug("[ConvertDiscImageToIso] Conversion timed out after 5 minutes");
                try
                {
                    process.Kill();
                }
                catch
                {
                    /* ignored */
                }

                return null;
            }

            if (process.ExitCode == 0 && File.Exists(tempIsoPath))
            {
                _logger.Debug("[ConvertDiscImageToIso] Conversion successful");
                return tempIsoPath;
            }

            _logger.Debug(
                $"[ConvertDiscImageToIso] DolphinTool failed. ExitCode: {process.ExitCode}. Error: {errorBuilder}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ConvertDiscImageToIso] Error converting disc image to ISO");
            return null;
        }
    }

    /// <summary>
    ///     Best-effort deletion of temporary conversion files (e.g. a partial BIN left behind by a failed extraction).
    /// </summary>
    private static void TryDeleteTempFiles(string tempCuePath, string tempBinPath)
    {
        try
        {
            if (File.Exists(tempCuePath)) File.Delete(tempCuePath);
            if (File.Exists(tempBinPath)) File.Delete(tempBinPath);
        }
        catch
        {
            // Best effort only; the OS will eventually clean up temp files.
        }
    }
}