using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Mame.DatCreator.Models;
using Mame.DatCreator.Services;

namespace Mame.DatCreator.Processors;

/// <summary>
///     Processes MAME software list XML files to extract machine information.
/// </summary>
/// <summary>
///     Processes MAME software list XML files to extract machine information.
/// </summary>
public static class MameSoftwareProcessor
{
    /// <summary>
    ///     Extracts machine information from all software list XML files in a folder.
    /// </summary>
    /// <param name="inputFolderPath">The path to the folder containing software list XMLs.</param>
    /// <param name="logger">The logger instance for output messages.</param>
    /// <returns>A list of machine information extracted from software lists.</returns>
    public static Task<IList<MachineInfo>> GetMachinesFromSoftwareFolderAsync(string inputFolderPath, WpfLogger logger)
    {
        return Task.Run<IList<MachineInfo>>(() =>
        {
            logger.Info($"Scanning for software list XMLs in: {inputFolderPath}");
            var files = Directory.GetFiles(inputFolderPath, "*.xml");
            if (files.Length == 0)
            {
                logger.Warning("No XML files found in the software list (hash) folder.");
                return new List<MachineInfo>();
            }

            logger.Info($"Found {files.Length} software list XML files to process.");

            var allSoftware = new ConcurrentBag<MachineInfo>();
            var warnings = new ConcurrentBag<string>();

            // MAME hash/*.xml files declare a DOCTYPE (softwarelist.dtd); Prohibit rejects
            // them all. Ignore skips the DTD and XmlResolver=null prevents external fetches.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };

            Parallel.ForEach(files, file =>
            {
                try
                {
                    using var reader = XmlReader.Create(file, settings);
                    var doc = XDocument.Load(reader);
                    var softwares = doc.Descendants("software")
                        .Select(static software => new MachineInfo
                        {
                            MachineName = software.Attribute("name")?.Value ?? "",
                            Description = software.Element("description")?.Value ?? "No Description"
                        });

                    foreach (var s in softwares) allSoftware.Add(s);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Skipping file '{Path.GetFileName(file)}' due to an error: {ex.Message}");
                }
            });

            // Log warnings after the parallel loop to avoid Dispatcher.Invoke
            // blocking thread pool threads.
            foreach (var warning in warnings) logger.Warning(warning);

            logger.Info($"Extracted {allSoftware.Count} entries from software lists.");
            return allSoftware.ToList();
        });
    }
}