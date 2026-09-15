using System.Xml;
using SimpleLauncher.Core.Services.RomHistory;
using Xunit;

namespace SimpleLauncher.Tests;

/// <summary>
///     Tests for <see cref="RomHistoryLoader" /> covering XML history file parsing,
///     system/software name matching, and error handling for corrupt or missing files.
/// </summary>
public class RomHistoryLoaderTests : IDisposable
{
    private readonly string _tempDir;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RomHistoryLoaderTests" /> class,
    ///     creating a temporary test directory.
    /// </summary>
    public RomHistoryLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    ///     Cleans up the temporary test directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);

        GC.SuppressFinalize(this);
    }

    private string CreateHistoryXml(string content)
    {
        var path = Path.Combine(_tempDir, "history.xml");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    ///     Verifies that FindEntry returns the correct entry when a system name match exists in the XML.
    /// </summary>
    [Fact]
    public void FindEntryValidXmlWithSystemMatchReturnsEntry()
    {
        const string xml = """
                           <?xml version="1.0" encoding="UTF-8"?>
                           <database>
                             <entry>
                               <systems>
                                 <system name="pacman" />
                               </systems>
                               <text>Pac-Man history text.</text>
                             </entry>
                           </database>
                           """;

        var path = CreateHistoryXml(xml);
        var result = RomHistoryLoader.FindEntry(path, "pacman");

        Assert.NotNull(result);
        Assert.Equal("Pac-Man history text.", result.Element("text")?.Value);
    }

    /// <summary>
    ///     Verifies that FindEntry returns the correct entry when a software item name match exists in the XML.
    /// </summary>
    [Fact]
    public void FindEntryValidXmlWithSoftwareMatchReturnsEntry()
    {
        const string xml = """
                           <?xml version="1.0" encoding="UTF-8"?>
                           <database>
                             <entry>
                               <software>
                                 <item name="galaga" />
                               </software>
                               <text>Galaga history text.</text>
                             </entry>
                           </database>
                           """;

        var path = CreateHistoryXml(xml);
        var result = RomHistoryLoader.FindEntry(path, "galaga");

        Assert.NotNull(result);
        Assert.Equal("Galaga history text.", result.Element("text")?.Value);
    }

    /// <summary>
    ///     Verifies that FindEntry returns null when no matching entry exists in the XML.
    /// </summary>
    [Fact]
    public void FindEntryValidXmlWithoutMatchReturnsNull()
    {
        const string xml = """
                           <?xml version="1.0" encoding="UTF-8"?>
                           <database>
                             <entry>
                               <systems>
                                 <system name="pacman" />
                               </systems>
                               <text>Pac-Man history text.</text>
                             </entry>
                           </database>
                           """;

        var path = CreateHistoryXml(xml);
        var result = RomHistoryLoader.FindEntry(path, "nonexistent");

        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindEntry returns null (instead of throwing to the UI) when the XML
    ///     file is malformed (CORE-29).
    /// </summary>
    [Fact]
    public void FindEntryCorruptedXmlReturnsNull()
    {
        const string xml = "<database><entry><unclosed>";
        var path = CreateHistoryXml(xml);

        var result = RomHistoryLoader.FindEntry(path, "anything");

        Assert.Null(result);
    }

    /// <summary>
    ///     Verifies that FindEntry returns null (instead of throwing to the UI) when the XML
    ///     file does not exist (CORE-29).
    /// </summary>
    [Fact]
    public void FindEntryMissingFileReturnsNull()
    {
        var missingPath = Path.Combine(_tempDir, "does_not_exist.xml");

        var result = RomHistoryLoader.FindEntry(missingPath, "anything");

        Assert.Null(result);
    }
}