using CHDSharp.Encoder;
using CHDSharp.Encoder.Models;
using SimpleLauncher.Core.Services.Converters;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for the managed CHD conversion path in <see cref="DiscConverter" /> (CHDSharp is
///     tried on every platform before the Windows chdman fallback). The CHD fixtures are built
///     with the CHDSharp encoder, so the tests are platform-independent and need no chdman.
/// </summary>
public class DiscConverterTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sl_discconv_" + Guid.NewGuid().ToString("N"));

    public DiscConverterTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static DiscConverter CreateConverter()
    {
        return new DiscConverter(TestDependencies.Logger().Object);
    }

    /// <summary>
    ///     Writes a minimal one-track MODE1/2352 CD image (CUE + BIN) usable by the encoder.
    /// </summary>
    // ReSharper disable once UnusedTupleComponentInReturnValue
    private (string CuePath, string BinPath) WriteMinimalCd()
    {
        var binPath = Path.Combine(_root, "game.bin");
        File.WriteAllBytes(binPath, new byte[2352 * 4]);

        var cuePath = Path.Combine(_root, "game.cue");
        File.WriteAllText(cuePath,
            "FILE \"game.bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");

        return (cuePath, binPath);
    }

    private string CreateCdChd()
    {
        var (cuePath, _) = WriteMinimalCd();
        var chdPath = Path.Combine(_root, "game.chd");
        ChdEncoder.EncodeCd(cuePath, chdPath);
        return chdPath;
    }

    private string CreateDvdChd()
    {
        var isoPath = Path.Combine(_root, "movie.iso");
        var isoBytes = new byte[4096 * 2];
        new Random(42).NextBytes(isoBytes);
        File.WriteAllBytes(isoPath, isoBytes);

        var chdPath = Path.Combine(_root, "movie.chd");
        ChdEncoder.EncodeRaw(isoPath, chdPath, hunkBytes: 4096, unitBytes: 2048,
            codecTags: [CodecTags.Zlib],
            options: new ChdEncodeOptions { Metadata = [MetadataWriter.BuildDvdMetadata()] });

        return chdPath;
    }

    [Fact]
    public async Task ConvertChdToCueBin_ExtractsCdImageToCueAndBin()
    {
        var chdPath = CreateCdChd();

        var convertedCue = await CreateConverter().ConvertChdToCueBinAsync(chdPath);

        Assert.NotNull(convertedCue);
        Assert.Equal(".cue", Path.GetExtension(convertedCue), ignoreCase: true);
        Assert.True(File.Exists(convertedCue));

        var convertedBin = Path.ChangeExtension(convertedCue, ".bin");
        Assert.True(File.Exists(convertedBin));
        Assert.Contains(Path.GetFileName(convertedBin), File.ReadAllText(convertedCue), StringComparison.Ordinal);

        File.Delete(convertedCue);
        File.Delete(convertedBin);
    }

    [Fact]
    public async Task ConvertChdToIso_ExtractsDvdImageByteForByte()
    {
        var chdPath = CreateDvdChd();
        var expectedIso = await File.ReadAllBytesAsync(Path.Combine(_root, "movie.iso"));

        var convertedIso = await CreateConverter().ConvertChdToIsoAsync(chdPath);

        Assert.NotNull(convertedIso);
        Assert.Equal(".iso", Path.GetExtension(convertedIso), ignoreCase: true);
        Assert.Equal(expectedIso, await File.ReadAllBytesAsync(convertedIso));

        File.Delete(convertedIso);
    }

    [Fact]
    public async Task ConvertChdToCueBin_ReturnsNullForNonChdFile()
    {
        var bogusPath = Path.Combine(_root, "not-a-chd.chd");
        await File.WriteAllTextAsync(bogusPath, "this is not a chd file");

        Assert.Null(await CreateConverter().ConvertChdToCueBinAsync(bogusPath));
    }
}
