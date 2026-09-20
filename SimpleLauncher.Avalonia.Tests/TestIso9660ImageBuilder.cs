using System.Text;

namespace SimpleLauncher.Avalonia.Tests;

/// <summary>
///     Builds a minimal ISO9660 image (primary volume descriptor, 2048-byte logical sectors)
///     in memory with one directory record per supplied path. File identifiers get the ';1'
///     version suffix ISO9660 appends, directory identifiers carry none. Used by the ISO reader
///     tests and as the source image for the CHDSharp encoder in the DOSBox CHD tests.
/// </summary>
internal static class TestIso9660ImageBuilder
{
    private const int SectorSize = 2048;
    private const int PvdSector = 16;
    private const int TerminatorSector = 17;
    private const int FirstDirectorySector = 18;

    public static byte[] Build(IReadOnlyDictionary<string, byte[]> files)
    {
        var directories = CollectDirectories(files.Keys);

        var directorySectors = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextSector = FirstDirectorySector;
        foreach (var directory in directories) directorySectors[directory] = nextSector++;

        var fileSectors = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in files.Keys.OrderBy(path => path, StringComparer.Ordinal))
        {
            var sectorCount = Math.Max(1, (files[path].Length + SectorSize - 1) / SectorSize);
            fileSectors[path] = nextSector;
            nextSector += sectorCount;
        }

        var image = new byte[nextSector * SectorSize];

        // Primary volume descriptor (sector 16) with the root directory record at offset 156.
        var pvd = new byte[SectorSize];
        pvd[0] = 1;
        WriteAscii(pvd, 1, "CD001");
        pvd[6] = 1;
        WriteAscii(pvd, 40, "TEST_IMAGE");
        WriteBoth32(pvd, 80, (uint)nextSector);
        WriteBoth16(pvd, 120, 1);
        WriteBoth16(pvd, 124, 1);
        WriteBoth16(pvd, 128, SectorSize);
        WriteBoth32(pvd, 132, 10);
        WriteRecord(pvd, 156, directorySectors[""], SectorSize, true, [0x00]);
        pvd.CopyTo(image, PvdSector * SectorSize);

        // Volume descriptor set terminator (sector 17).
        var terminator = new byte[SectorSize];
        terminator[0] = 255;
        WriteAscii(terminator, 1, "CD001");
        terminator[6] = 1;
        terminator.CopyTo(image, TerminatorSector * SectorSize);

        // One sector per directory with its ".", "..", child directory and file records.
        foreach (var directory in directories)
        {
            var sector = new byte[SectorSize];
            var offset = 0;
            offset += WriteRecord(sector, offset, directorySectors[directory], SectorSize, true, [0x00]);
            offset += WriteRecord(sector, offset, directorySectors[ParentOf(directory)], SectorSize, true, [0x01]);

            foreach (var child in directories.Where(child => child.Length > 0 &&
                                                             string.Equals(ParentOf(child), directory, StringComparison.Ordinal)))
                offset += WriteRecord(sector, offset, directorySectors[child], SectorSize, true,
                    Encoding.ASCII.GetBytes(NameOf(child)));

            foreach (var path in files.Keys.OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!string.Equals(ParentOf(path), directory, StringComparison.Ordinal)) continue;
                offset += WriteRecord(sector, offset, fileSectors[path], files[path].Length, false,
                    Encoding.ASCII.GetBytes(NameOf(path) + ";1"));
            }

            sector.CopyTo(image, directorySectors[directory] * SectorSize);
        }

        foreach (var (path, content) in files) content.CopyTo(image, fileSectors[path] * SectorSize);

        return image;
    }

    /// <summary>Returns every directory path (including the root "") that contains a file.</summary>
    private static List<string> CollectDirectories(IEnumerable<string> filePaths)
    {
        var directories = new HashSet<string>(StringComparer.Ordinal) { "" };
        foreach (var path in filePaths)
        {
            var slash = path.LastIndexOf('/');
            while (slash > 0)
            {
                directories.Add(path[..slash]);
                slash = path.LastIndexOf('/', slash - 1);
            }
        }

        return directories.OrderBy(directory => directory, StringComparer.Ordinal).ToList();
    }

    private static string ParentOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    private static string NameOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }

    private static int WriteRecord(byte[] sector, int offset, int lba, int dataLength, bool isDirectory, byte[] name)
    {
        // ECMA-119 9.1.12: a zero padding byte follows file identifiers of even length, keeping
        // the record length even; the padding is not included in the identifier length field.
        var padding = name.Length % 2 == 0 ? 1 : 0;
        var length = 33 + name.Length + padding;
        if (offset + length > SectorSize) throw new InvalidOperationException("The test directory sector is full");

        sector[offset] = (byte)length;
        WriteBoth32(sector, offset + 2, (uint)lba);
        WriteBoth32(sector, offset + 10, (uint)dataLength);
        sector[offset + 25] = isDirectory ? (byte)0x02 : (byte)0x00;
        WriteBoth16(sector, offset + 28, 1);
        sector[offset + 32] = (byte)name.Length;
        name.CopyTo(sector, offset + 33);
        return length;
    }

    private static void WriteAscii(byte[] buffer, int offset, string value)
    {
        Encoding.ASCII.GetBytes(value).CopyTo(buffer, offset);
    }

    private static void WriteBoth16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static void WriteBoth32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
        buffer[offset + 4] = (byte)(value >> 24);
        buffer[offset + 5] = (byte)(value >> 16);
        buffer[offset + 6] = (byte)(value >> 8);
        buffer[offset + 7] = (byte)value;
    }
}
