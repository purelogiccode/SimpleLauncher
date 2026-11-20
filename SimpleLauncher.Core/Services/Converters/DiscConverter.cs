using CHDSharp;
using CHDSharp.Models;
using PBPSharp;
using PBPSharp.Models;
using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.Converters;

/// <summary>
///     Converts CHD and PBP disc images to ISO or CUE/BIN with the managed CHDSharp and
///     PBPSharp libraries, so no external converter executable is required.
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
    ///     Converts a CHD disc image to an ISO file with the managed CHDSharp decoder.
    /// </summary>
    /// <param name="chdPath">The path of the CHD file to convert.</param>
    /// <returns>The path of the converted ISO file, or null if the conversion failed.</returns>
    public async Task<string?> ConvertChdToIsoAsync(string chdPath)
    {
        return await Task.Run(() => TryExtractWithChdSharp(chdPath, ".iso", "[ConvertChdToIso]"));
    }

    /// <summary>
    ///     Converts a CHD disc image to a CUE/BIN pair with the managed CHDSharp decoder.
    /// </summary>
    /// <param name="chdPath">The path of the CHD file to convert.</param>
    /// <returns>The path of the converted CUE file, or null if the conversion failed.</returns>
    public async Task<string?> ConvertChdToCueBinAsync(string chdPath)
    {
        return await Task.Run(() => TryExtractWithChdSharp(chdPath, ".cue", "[ConvertChdToCueBin]"));
    }

    /// <summary>
    ///     Extracts a CHD with the managed CHDSharp decoder into the conversion temp folder:
    ///     CD images become a CUE/BIN pair and DVD images become an ISO. Returns the path of
    ///     the requested file, or null when the CHD is not the expected media type, cannot be
    ///     decoded, or the extraction fails.
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
    ///     The pattern has no dot after the GUID base name: multi-track extractions emit
    ///     per-track files such as <c>&lt;base&gt; (1).bin</c>.
    /// </summary>
    private static void CleanupTempBaseName(string baseName)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(TempFolder, baseName + "*"))
                TryDeleteFile(file);
        }
        catch
        {
            // Best effort only; the OS will eventually clean up temp files.
        }
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