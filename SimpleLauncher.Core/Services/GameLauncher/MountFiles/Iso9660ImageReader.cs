using SimpleLauncher.Core.Interfaces;

namespace SimpleLauncher.Core.Services.GameLauncher.MountFiles;

/// <summary>
///     Lists the files stored in a cooked ISO9660 disc image (2048-byte logical sectors).
///     That is the format of the data track written by chdman/CHDSharp 'extractcd' (cooked)
///     and of DVD images, which the DOSBox launch strategy mounts inside DOSBox with
///     'imgmount' on platforms without a virtual-drive backend (Linux/macOS).
///     Only the primary volume descriptor is read: it contains the 8.3 names DOSBox itself
///     shows for the mounted image (Joliet/rock-ridge names are ignored on purpose).
/// </summary>
public static class Iso9660ImageReader
{
    private const int SectorSize = 2048;
    private const int FirstVolumeDescriptorSector = 16;
    private const int MaxVolumeDescriptorSectors = 16;
    private const int MaxDirectoryDepth = 32;
    private const int MaxListedEntries = 20000;

    /// <summary>
    ///     Returns the relative file paths inside the image: ISO primary names, '/'-separated
    ///     and without the ';version' suffix (for example <c>SUB/PLAY.BAT</c>). Returns an empty
    ///     list when the file is missing or is not a readable ISO9660 image.
    /// </summary>
    /// <param name="imagePath">The path of the cooked ISO9660 image (.bin data track or .iso).</param>
    /// <param name="logger">The logger used to record unreadable-image conditions.</param>
    public static List<string> ListFiles(string imagePath, ILogger logger)
    {
        var files = new List<string>();

        try
        {
            using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var root = FindRootDirectoryRecord(stream, imagePath, logger);
            if (root == null) return files;

            var listedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<(uint Extent, uint Length, string Prefix, int Depth)>();
            pending.Push((root.Value.Extent, root.Value.Length, "", 0));

            while (pending.Count > 0 && files.Count < MaxListedEntries)
            {
                var (extent, length, prefix, depth) = pending.Pop();
                if (depth > MaxDirectoryDepth || length == 0) continue;

                foreach (var entry in ReadDirectory(stream, extent, length))
                {
                    if (entry.Name is null) continue;

                    var path = prefix.Length == 0 ? entry.Name : $"{prefix}/{entry.Name}";
                    if (entry.IsDirectory)
                        pending.Push((entry.Extent, entry.Length, path, depth + 1));
                    else if (listedPaths.Add(path))
                        files.Add(path);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Information($"[Iso9660ImageReader] Unable to read disc image '{imagePath}': {ex.Message}");
        }

        return files;
    }

    /// <summary>
    ///     Scans the volume descriptor sequence for the primary volume descriptor and returns
    ///     its root directory record (extent LBA and data length).
    /// </summary>
    private static (uint Extent, uint Length)? FindRootDirectoryRecord(FileStream stream, string imagePath, ILogger logger)
    {
        for (var i = 0; i < MaxVolumeDescriptorSectors; i++)
        {
            var sector = ReadSector(stream, FirstVolumeDescriptorSector + i);
            if (sector == null) break;
            if (sector[0] == 255) break; // volume descriptor set terminator
            if (!HasIsoSignature(sector) || sector[0] != 1) continue;

            // The root directory record sits at offset 156 of the primary volume descriptor.
            var recordLength = sector[156];
            if (recordLength < 34 || 156 + recordLength > SectorSize) break;
            var record = sector.AsSpan(156, recordLength);
            return (ReadLittleEndian32(record, 2), ReadLittleEndian32(record, 10));
        }

        logger.Information($"[Iso9660ImageReader] '{imagePath}' is not a readable ISO9660 disc image");
        return null;
    }

    private static bool HasIsoSignature(ReadOnlySpan<byte> sector)
    {
        return sector.Length >= 6 && sector[1] == (byte)'C' && sector[2] == (byte)'D' && sector[3] == (byte)'0' &&
               sector[4] == (byte)'0' && sector[5] == (byte)'1';
    }

    /// <summary>
    ///     Reads the directory records of one directory, sector by sector: records never cross
    ///     a sector boundary, and a zero length byte marks the padding at the end of a sector.
    /// </summary>
    private static IEnumerable<(string? Name, bool IsDirectory, uint Extent, uint Length)> ReadDirectory(
        FileStream stream, uint extent, uint length)
    {
        var sectorLba = extent;
        long remaining = length;

        while (remaining > 0)
        {
            var sector = ReadSector(stream, sectorLba);
            if (sector == null) yield break;
            sectorLba++;

            var sectorLength = (int)Math.Min(SectorSize, remaining);
            var offset = 0;
            while (offset + 34 <= sectorLength)
            {
                int recordLength = sector[offset];
                if (recordLength < 34 || offset + recordLength > SectorSize) break;

                var record = sector.AsSpan(offset, recordLength);
                var extentLba = ReadLittleEndian32(record, 2);
                var dataLength = ReadLittleEndian32(record, 10);
                var flags = record[25];
                int nameLength = record[32];

                if (nameLength > 0 && 33 + nameLength <= recordLength)
                {
                    var nameBytes = record.Slice(33, nameLength);
                    var isDirectory = (flags & 0x02) != 0;

                    // Names 0x00 (".") and 0x01 ("..") are the self/parent entries.
                    if (nameBytes[0] > 1)
                        yield return (DecodeName(nameBytes, isDirectory), isDirectory, extentLba, dataLength);
                }

                offset += recordLength;
            }

            remaining -= sectorLength;
        }
    }

    /// <summary>
    ///     Decodes a directory record name: strips the ';version' suffix ISO9660 appends to
    ///     file identifiers. Directory identifiers carry no version suffix.
    /// </summary>
    private static string DecodeName(ReadOnlySpan<byte> nameBytes, bool isDirectory)
    {
        var length = nameBytes.Length;
        if (!isDirectory)
        {
            var separator = nameBytes.IndexOf((byte)';');
            if (separator >= 0) length = separator;
        }

        var chars = new char[length];
        for (var i = 0; i < length; i++) chars[i] = (char)nameBytes[i];
        return new string(chars);
    }

    private static uint ReadLittleEndian32(ReadOnlySpan<byte> buffer, int offset)
    {
        return (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) |
                      (buffer[offset + 3] << 24));
    }

    private static byte[]? ReadSector(FileStream stream, long lba)
    {
        var offset = lba * SectorSize;
        if (offset < 0 || offset + SectorSize > stream.Length) return null;

        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[SectorSize];
        var read = stream.ReadAtLeast(buffer, SectorSize, false);
        return read == SectorSize ? buffer : null;
    }
}
