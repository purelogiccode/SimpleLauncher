using System.Xml;
using System.Xml.Linq;
using Mame.DatCreator.Models;
using Mame.DatCreator.Services;

namespace Mame.DatCreator.Processors;

/// <summary>
///     Processes the MAME full driver XML file to extract machine information.
/// </summary>
/// <summary>
///     Processes MAME full driver XML files to extract machine information.
/// </summary>
public static class MameFullProcessor
{
    /// <summary>
    ///     Extracts machine information from a MAME full driver XML file.
    /// </summary>
    /// <param name="inputFilePath">The path to the MAME full driver XML file.</param>
    /// <param name="logger">The logger instance for output messages.</param>
    /// <returns>A list of machine information extracted from the XML.</returns>
    public static Task<IList<MachineInfo>> GetMachinesFromFullXmlAsync(string inputFilePath, WpfLogger logger)
    {
        return Task.Run(() =>
        {
            try
            {
                logger.Info($"Loading MAME full driver XML from: {inputFilePath}");
                // MAME's -listxml output starts with an internal DTD subset; Prohibit would
                // reject every stock file. Ignore skips the DTD (and never resolves external
                // entities because XmlResolver is null) while still parsing the document.
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
                using var reader = XmlReader.Create(inputFilePath, settings);
                var inputDoc = XDocument.Load(reader);
                IList<MachineInfo> machines = new List<MachineInfo>();

                var machineElements = inputDoc.Descendants("machine").ToList();
                logger.Info($"Found {machineElements.Count} machine entries in the MAME full driver XML.");

                foreach (var m in machineElements)
                {
                    machines.Add(new MachineInfo
                    {
                        MachineName = m.Attribute("name")?.Value ?? "",
                        Description = m.Element("description")?.Value ?? ""
                    });
                }

                return machines;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to process MAME full driver XML: {inputFilePath}", ex);
                throw;
            }
        });
    }
}