using System.Xml;
using System.Xml.Linq;
using MessagePack;
using SimpleLauncher.Core.Models;

namespace SimpleLauncher.Core.Services.RomHistory;

/// <summary>
///     Provides methods for loading and querying the local ROM history database (history.dat or history.xml).
/// </summary>
public static class RomHistoryLoader
{
    /// <summary>
    ///     Searches for an entry matching <paramref name="romName" /> in history.dat (MessagePack) first,
    ///     then falls back to history.xml if the .dat file is not available.
    /// </summary>
    /// <param name="historyFilePath">Full path to the history.xml file.</param>
    /// <param name="romName">The ROM name to search for.</param>
    /// <returns>The matching <see cref="XElement" /> entry, or <c>null</c> if not found.</returns>
    public static XElement? FindEntry(string historyFilePath, string romName)
    {
        var datFilePath = Path.ChangeExtension(historyFilePath, ".dat");

        // A .dat deleted between the check and the read, or a corrupt one, must fall
        // back to XML instead of throwing to the UI (CORE-29). A successful read —
        // even with no match — returns directly so a miss doesn't pay for a full
        // XML parse.
        if (File.Exists(datFilePath))
        {
            try
            {
                return FindEntryFromDat(datFilePath, romName);
            }
            catch (Exception ex)
            {
                Log.Debug($"[RomHistoryLoader] Failed to read '{datFilePath}', falling back to XML: {ex.Message}");
            }
        }

        try
        {
            return FindEntryFromXml(historyFilePath, romName);
        }
        catch (Exception ex)
        {
            // Missing or unreadable XML means "no entry", not an exception for the UI (CORE-29).
            Log.Debug($"[RomHistoryLoader] Failed to read '{historyFilePath}': {ex.Message}");
            return null;
        }
    }

    private static XElement? FindEntryFromDat(string datFilePath, string romName)
    {
        var binaryData = File.ReadAllBytes(datFilePath);
        var history = MessagePackSerializer.Deserialize<HistoryData>(binaryData);

        if (history?.Entries == null) return null;

        foreach (var entry in history.Entries)
        {
            if ((entry.Systems?.SystemItems != null &&
                 entry.Systems.SystemItems.Any(s => string.Equals(s.Name, romName, StringComparison.Ordinal))) ||
                (entry.Software?.Items != null &&
                 entry.Software.Items.Any(i => string.Equals(i.Name, romName, StringComparison.Ordinal))))
            {
                return BuildEntryXElement(entry);
            }
        }

        return null;
    }

    private static XElement BuildEntryXElement(EntryData entry)
    {
        var element = new XElement("entry");

        if (entry.Software?.Items is { Length: > 0 })
        {
            var software = new XElement("software");
            foreach (var item in entry.Software.Items)
            {
                var itemElement = new XElement("item");
                if (item.List != null) itemElement.Add(new XAttribute("list", item.List));
                if (item.Name != null) itemElement.Add(new XAttribute("name", item.Name));
                if (item.Game != null) itemElement.Add(new XAttribute("game", item.Game));
                software.Add(itemElement);
            }

            element.Add(software);
        }

        if (entry.Systems?.SystemItems is { Length: > 0 })
        {
            var systems = new XElement("systems");
            foreach (var sys in entry.Systems.SystemItems)
            {
                var sysElement = new XElement("system");
                if (sys.Name != null) sysElement.Add(new XAttribute("name", sys.Name));
                if (sys.Game != null) sysElement.Add(new XAttribute("game", sys.Game));
                systems.Add(sysElement);
            }

            element.Add(systems);
        }

        if (entry.Text != null) element.Add(new XElement("text", entry.Text));

        return element;
    }

    private static XElement? FindEntryFromXml(string historyFilePath, string romName)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(historyFilePath, settings);
        var doc = XDocument.Load(reader, LoadOptions.None);

        return doc.Descendants("entry")
                   .FirstOrDefault(e => e.Element("systems")?.Elements("system")
                       .Any(system =>
                           string.Equals(system.Attribute("name")?.Value, romName, StringComparison.Ordinal)) == true)
               ?? doc.Descendants("entry")
                   .FirstOrDefault(e => e.Element("software")?.Elements("item")
                                            .Any(item => string.Equals(item.Attribute("name")?.Value, romName,
                                                StringComparison.Ordinal)) ==
                                        true);
    }
}