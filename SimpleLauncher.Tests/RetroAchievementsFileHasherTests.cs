using System.ComponentModel;
using System.Reflection;
using System.Text;
using SimpleLauncher.Core.Services.RetroAchievements;
using SimpleLauncher.Tests.TestHelpers;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="RetroAchievementsFileHasher" /> covering the hash calculation
///     delegated to the RetroAchievementsSharp CLI tool (whole-file hashes, header stripping,
///     N64 byte-swapping, arcade filename hashing, Arduboy line-ending normalization, and
///     graceful failures for unsupported inputs).
/// </summary>
public class RetroAchievementsFileHasherTests : IDisposable
{
    private readonly RetroAchievementsFileHasher _hasher = new(
        new NoOpLogger(),
        new RetroAchievementsSystemMatcher(new NoOpLogger()));

    private readonly string _testDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RetroAchievementsFileHasherTests" /> class,
    ///     installing the service provider mock and creating a temporary test directory.
    /// </summary>
    public RetroAchievementsFileHasherTests()
    {
        ServiceProviderMock.Install();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SL_RAHashTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>
    ///     Cleans up the temporary test directory and restores the service provider mock.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Best-effort cleanup
        }

        ServiceProviderMock.Restore();
        GC.SuppressFinalize(this);
    }

    private string CreateTempFile(string relativePath, byte[] content)
    {
        var fullPath = Path.Combine(_testDirectory, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(fullPath, content);
        return fullPath;
    }

    private string CreateTempFile(string relativePath, string content, Encoding? encoding = null)
    {
        var fullPath = Path.Combine(_testDirectory, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content, encoding ?? Encoding.UTF8);
        return fullPath;
    }

    /// <summary>
    ///     Verifies that Game Boy hashing returns the expected whole-file MD5 hash for known content.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncGameBoyReturnsExpectedHash()
    {
        var content = "Hello World"u8.ToArray();
        var filePath = CreateTempFile("test.gb", content);

        var result = await _hasher.CalculateHashAsync(filePath, "game boy");

        Assert.Equal("b10a8db164e0754105b7a99be72e3fe5", result);
    }

    /// <summary>
    ///     Verifies that Game Boy hashing returns the MD5 hash for an empty file.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncGameBoyEmptyFileReturnsExpectedHash()
    {
        var filePath = CreateTempFile("empty.gb", []);

        var result = await _hasher.CalculateHashAsync(filePath, "game boy");

        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result);
    }

    /// <summary>
    ///     Verifies that Arcade hashing returns a 32-character hex hash for a valid file path.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncArcadeReturnsExpectedHash()
    {
        var filePath = CreateTempFile("game.zip", "PK"u8.ToArray());

        var result = await _hasher.CalculateHashAsync(filePath, "arcade");

        Assert.NotNull(result);
        Assert.Equal(32, result.Length);
    }

    /// <summary>
    ///     Verifies that Arcade hashing produces the same hash for files with the same name
    ///     but different paths or extensions.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncArcadeSameFilenameWithoutExtensionProducesSameHash()
    {
        var filePath1 = CreateTempFile("mygame.zip", "PK"u8.ToArray());
        var filePath2 = CreateTempFile("mygame.7z", "7z"u8.ToArray());

        var hash1 = await _hasher.CalculateHashAsync(filePath1, "arcade");
        var hash2 = await _hasher.CalculateHashAsync(filePath2, "arcade");

        Assert.Equal(hash1, hash2);
    }

    /// <summary>
    ///     Verifies that Arcade hashing produces different hashes for different file names.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncArcadeDifferentFilenamesProduceDifferentHashes()
    {
        var filePath1 = CreateTempFile("game1.zip", "PK"u8.ToArray());
        var filePath2 = CreateTempFile("game2.zip", "PK"u8.ToArray());

        var hash1 = await _hasher.CalculateHashAsync(filePath1, "arcade");
        var hash2 = await _hasher.CalculateHashAsync(filePath2, "arcade");

        Assert.NotEqual(hash1, hash2, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Verifies that Arduboy hashing normalizes CRLF and LF line endings to produce the same hash.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncArduboyNormalizesLineEndings()
    {
        var filePath1 = CreateTempFile("arduboy_crlf.hex", "hello\r\nworld\n");
        var filePath2 = CreateTempFile("arduboy_lf.hex", "hello\nworld\n");

        var hash1 = await _hasher.CalculateHashAsync(filePath1, "arduboy");
        var hash2 = await _hasher.CalculateHashAsync(filePath2, "arduboy");

        Assert.NotNull(hash1);
        Assert.NotNull(hash2);
        Assert.Equal(hash1, hash2);
    }

    /// <summary>
    ///     Verifies that .z64 N64 ROMs are hashed directly (big endian, no byte swapping).
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncN64Z64ReturnsStandardMd5()
    {
        var content = new byte[] { 0x80, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
        var filePath = CreateTempFile("game.z64", content);

        var result = await _hasher.CalculateHashAsync(filePath, "nintendo 64");

        Assert.Equal("5bdfae9b422a1fa9ff5964c65d5904ce", result);
    }

    /// <summary>
    ///     Verifies that .v64 N64 ROMs are 16-bit byte-swapped before hashing.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncN64V64ReturnsByteSwappedMd5()
    {
        var content = new byte[] { 0x37, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
        var filePath = CreateTempFile("game.v64", content);

        var result = await _hasher.CalculateHashAsync(filePath, "nintendo 64");

        Assert.Equal("b5ee2c7bb34f654a6cdfaad7320608e5", result);
    }

    /// <summary>
    ///     Verifies that .n64 N64 ROMs are 32-bit byte-swapped before hashing.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncN64N64ReturnsByteSwappedMd5()
    {
        var content = new byte[] { 0x40, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
        var filePath = CreateTempFile("game.n64", content);

        var result = await _hasher.CalculateHashAsync(filePath, "nintendo 64");

        Assert.Equal("af75f3d8964941aaea7c83aa8408067d", result);
    }

    /// <summary>
    ///     Verifies that N64 hashing returns null for files that are not valid N64 ROMs.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncN64InvalidRomReturnsNull()
    {
        var content = new byte[] { 0x99, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
        var filePath = CreateTempFile("game.rom", content);

        var result = await _hasher.CalculateHashAsync(filePath, "nintendo 64");

        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that NES ROMs with a 16-byte header are hashed after stripping the header.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncNesStripsHeader()
    {
        var header = new byte[16];
        header[0] = (byte)'N';
        header[1] = (byte)'E';
        header[2] = (byte)'S';
        header[3] = 0x1A;
        var filePath = CreateTempFile("game.nes", [.. header, .. "Hello World"u8.ToArray()]);

        var result = await _hasher.CalculateHashAsync(filePath, "nintendo entertainment system");

        Assert.Equal("b10a8db164e0754105b7a99be72e3fe5", result);
    }

    /// <summary>
    ///     Verifies that NES ROMs without a header are hashed entirely.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncNesWithoutHeaderHashesWholeFile()
    {
        var filePath = CreateTempFile("game.nes", "Hello World"u8.ToArray());

        var result = await _hasher.CalculateHashAsync(filePath, "nintendo entertainment system");

        Assert.Equal("b10a8db164e0754105b7a99be72e3fe5", result);
    }

    /// <summary>
    ///     Verifies that 3DS files return null when no decryption keys are available.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsync3DsWithoutKeysReturnsNull()
    {
        var filePath = CreateTempFile("game.cia", [1, 2, 3, 4, 5, 6, 7, 8]);

        var result = await _hasher.CalculateHashAsync(filePath, "nintendo 3ds");

        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that hashing returns null for systems without a known console ID.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncUnknownSystemReturnsNull()
    {
        var filePath = CreateTempFile("game.bin", [1, 2, 3, 4]);

        var result = await _hasher.CalculateHashAsync(filePath, "some unknown system");

        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies the single-source gate rejects systems the CLI cannot hash — the
    ///     "unsupported" pseudo-system and Wii U (no FromFile hasher, id 20) — before
    ///     the CLI tool is ever invoked.
    /// </summary>
    /// <param name="systemName">The unsupported system name.</param>
    [Theory]
    [InlineData("standalone")]
    [InlineData("unsupported")]
    [InlineData("Wii U")]
    public async Task CalculateHashAsyncUnsupportedSystemReturnsNull(string systemName)
    {
        var filePath = CreateTempFile("game.bin", [1, 2, 3, 4]);

        var result = await _hasher.CalculateHashAsync(filePath, systemName);

        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that hashing returns null for files that do not exist.
    /// </summary>
    [Fact]
    public async Task CalculateHashAsyncMissingFileReturnsNull()
    {
        var missingFile = Path.Combine(_testDirectory, "does_not_exist.bin");

        var result = await _hasher.CalculateHashAsync(missingFile, "game boy");

        Assert.Null(result);
    }

    /// <summary>
    ///     Batch hashing: several files of one system are hashed in a single CLI
    ///     invocation and every hash matches the single-file result.
    /// </summary>
    [Fact]
    public async Task CalculateHashesAsyncMultipleFilesHashesAll()
    {
        var file1 = CreateTempFile("batch/A.gb", "Hello World"u8.ToArray());
        var file2 = CreateTempFile("batch/B.gb", "The quick brown fox jumps over the lazy dog"u8.ToArray());

        var result = await _hasher.CalculateHashesAsync([file1, file2], "game boy");

        Assert.Equal(2, result.Count);
        Assert.Equal("b10a8db164e0754105b7a99be72e3fe5", result[file1]);
        Assert.Equal("9e107d9d372bb6826bd81d3542a419d6", result[file2]);
    }

    /// <summary>
    ///     The CLI stops at the first file it cannot hash. The hasher must resume the
    ///     untouched tail so one unhashable file (e.g. an unsupported format) never
    ///     hides the rest of the library.
    /// </summary>
    [Fact]
    public async Task CalculateHashesAsyncUnhashableFileInTheMiddleStillHashesTheRest()
    {
        var good1 = CreateTempFile("resume/A.gb", "Hello World"u8.ToArray());
        var unhashable = Path.Combine(_testDirectory, "resume", "missing.bin");
        var good2 = CreateTempFile("resume/B.gb", []);

        var result = await _hasher.CalculateHashesAsync([good1, unhashable, good2], "game boy");

        Assert.Equal(2, result.Count);
        Assert.Equal("b10a8db164e0754105b7a99be72e3fe5", result[good1]);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result[good2]);
        Assert.False(result.ContainsKey(unhashable));
    }

    /// <summary>
    ///     The CLI prints only the file name next to each hash, so files with the same
    ///     name in different folders must still be mapped back to their full paths
    ///     (positional mapping) with their own content hash.
    /// </summary>
    [Fact]
    public async Task CalculateHashesAsyncDuplicateBasenamesInDifferentDirectoriesHashesBoth()
    {
        var file1 = CreateTempFile("dirA/game.gb", "Hello World"u8.ToArray());
        var file2 = CreateTempFile("dirB/game.gb", []);

        var result = await _hasher.CalculateHashesAsync([file1, file2], "game boy");

        Assert.Equal(2, result.Count);
        Assert.Equal("b10a8db164e0754105b7a99be72e3fe5", result[file1]);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result[file2]);
    }

    /// <summary>
    ///     Bug 67780: the permission-denied classifier must recognize the Win32Exception
    ///     Process.Start throws on Linux when the bundled CLI lost its executable bit —
    ///     native code 13 (EACCES) or a "Permission denied" message, including via an
    ///     inner exception — so that catch logs Information instead of Error.
    /// </summary>
    /// <param name="useEacces">
    ///     True to use native code 13 (EACCES); false for an unrelated Win32 error code.
    /// </param>
    [Theory]
    [InlineData(true)] // native code 13 (EACCES), as thrown on Linux
    [InlineData(false)] // unrelated Win32 error code
    public void IsPermissionDenied_Win32NativeCodes_ClassifiesAsExpected(bool useEacces)
    {
        var exception = useEacces
            ? new Win32Exception(13)
            : new Win32Exception(2, "The system cannot find the file specified.");

        Assert.Equal(useEacces, InvokeIsPermissionDenied(exception));
    }

    /// <summary>
    ///     Bug 67780: the exact exception shape from the report — "Permission denied" in
    ///     the Win32Exception message — must be classified as permission denied.
    /// </summary>
    [Fact]
    public void IsPermissionDenied_PermissionDeniedMessage_ClassifiesAsPermissionDenied()
    {
        var exception = new Win32Exception(1,
            "An error occurred trying to start process 'x' with working directory 'y'. Permission denied");

        Assert.True(InvokeIsPermissionDenied(exception));
    }

    /// <summary>
    ///     Bug 67780: a wrapped Win32Exception (inner-exception chain) is still detected.
    /// </summary>
    [Fact]
    public void IsPermissionDenied_InnerWin32Exception_ClassifiesAsPermissionDenied()
    {
        var exception = new InvalidOperationException("outer", new Win32Exception(13));

        Assert.True(InvokeIsPermissionDenied(exception));
    }

    /// <summary>
    ///     UnauthorizedAccessException is also classified as permission denied.
    /// </summary>
    [Fact]
    public void IsPermissionDenied_UnauthorizedAccess_ClassifiesAsPermissionDenied()
    {
        Assert.True(InvokeIsPermissionDenied(new UnauthorizedAccessException()));
    }

    /// <summary>
    ///     An unrelated exception must not be classified as permission denied (it stays
    ///     on the Error path).
    /// </summary>
    [Fact]
    public void IsPermissionDenied_UnrelatedException_ClassifiesAsNotPermissionDenied()
    {
        Assert.False(InvokeIsPermissionDenied(new InvalidOperationException("boom")));
    }

    private static bool InvokeIsPermissionDenied(Exception exception)
    {
        var method = typeof(RetroAchievementsFileHasher).GetMethod("IsPermissionDenied",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [exception]));
    }
}