using SimpleLauncher.Core.Services.GameLauncher.MountFiles;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Tests for <see cref="Iso9660ImageReader" />, which lists the executables of a cooked
///     ISO9660 disc image (chdman/CHDSharp 'extractcd' data tracks and DVD ISOs) for the
///     non-Windows DOSBox CHD fallback (LB-02).
/// </summary>
public class Iso9660ImageReaderTests
{
    [Fact]
    public void ListFiles_ReturnsRootAndNestedIsoNamesWithoutVersionSuffix()
    {
        var imagePath = WriteImage(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["GAME.EXE"] = new byte[1234],
            ["SUB/PLAY.BAT"] = new byte[99],
            ["DEEP/NESTED/RUN.COM"] = new byte[10]
        });

        try
        {
            var files = Iso9660ImageReader.ListFiles(imagePath, Log.Logger);

            Assert.Equal(3, files.Count);
            Assert.Contains("GAME.EXE", files, StringComparer.Ordinal);
            Assert.Contains("SUB/PLAY.BAT", files, StringComparer.Ordinal);
            Assert.Contains("DEEP/NESTED/RUN.COM", files, StringComparer.Ordinal);
            Assert.DoesNotContain(files, file => file.Contains(';'));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void ListFiles_NotAnIsoImage_ReturnsEmpty()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"sl-notiso-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(imagePath, new byte[64 * 1024]);

        try
        {
            Assert.Empty(Iso9660ImageReader.ListFiles(imagePath, Log.Logger));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void ListFiles_MissingFile_ReturnsEmpty()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"sl-missing-{Guid.NewGuid():N}.bin");
        Assert.Empty(Iso9660ImageReader.ListFiles(imagePath, Log.Logger));
    }

    [Fact]
    public void ListFiles_FileLargerThanOneSector_IsListed()
    {
        var imagePath = WriteImage(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["BIG.DAT"] = new byte[5000] });

        try
        {
            Assert.Equal(["BIG.DAT"], Iso9660ImageReader.ListFiles(imagePath, Log.Logger));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private static string WriteImage(IReadOnlyDictionary<string, byte[]> files)
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"sl-iso-{Guid.NewGuid():N}.iso");
        File.WriteAllBytes(imagePath, TestIso9660ImageBuilder.Build(files));
        return imagePath;
    }
}

