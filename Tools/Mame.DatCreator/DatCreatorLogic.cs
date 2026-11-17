using System.IO;
using System.Xml.Linq;
using Mame.DatCreator.Models;
using Mame.DatCreator.Processors;
using Mame.DatCreator.Services;
using MessagePack;

namespace Mame.DatCreator;

/// <summary>
///     Contains the core logic for creating merged MAME DAT files.
/// </summary>
/// <summary>
///     Contains logic for creating merged MAME DAT files from XML sources.
/// </summary>
public class DatCreatorLogic
{
    private readonly WpfLogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DatCreatorLogic" /> class.
    /// </summary>
    /// <param name="logger">The logger instance for output messages.</param>
    public DatCreatorLogic(WpfLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Creates a merged DAT file from MAME full driver XML and software list folder.
    /// </summary>
    /// <param name="fullXmlPath">Path to the MAME full driver XML file.</param>
    /// <param name="hashFolderPath">Path to the MAME hash folder containing software list XMLs.</param>
    /// <param name="outputXmlPath">Path to save the output XML and DAT files.</param>
    public async Task CreateMergedDatAsync(string fullXmlPath, string hashFolderPath, string outputXmlPath)
    {
        try
        {
            if (PathsEqual(fullXmlPath, outputXmlPath))
            {
                throw new InvalidOperationException(
                    "The output XML path must be different from the input MAME XML path: " +
                    "saving the merged list there would destroy the source file.");
            }

            _logger.Info("--- Starting Process 1: MAME Full List ---");
            var fullList = await MameFullProcessor.GetMachinesFromFullXmlAsync(fullXmlPath, _logger);

            _logger.Info("--- Starting Process 2: MAME Software List ---");
            var softwareList = await MameSoftwareProcessor.GetMachinesFromSoftwareFolderAsync(hashFolderPath, _logger);

            _logger.Info("--- Starting Process 3: Merging Lists ---");
            var uniqueMachines = new Dictionary<string, MachineInfo>(StringComparer.OrdinalIgnoreCase);

            // Add machines from the full list FIRST (full list has priority)
            foreach (var machine in fullList)
            {
                if (!string.IsNullOrEmpty(machine.MachineName))
                    uniqueMachines.TryAdd(machine.MachineName, machine);
            }

            _logger.Info($"After processing full list, there are {uniqueMachines.Count} unique machines.");

            // Add machines from software list ONLY if they don't already exist
            var skippedCount = 0;
            var addedCount = 0;

            foreach (var machine in softwareList)
            {
                if (!string.IsNullOrEmpty(machine.MachineName))
                {
                    if (!uniqueMachines.TryAdd(machine.MachineName, machine))
                    {
                        // Skip - full list entry takes priority
                        skippedCount++;
                    }
                    else
                    {
                        addedCount++;
                    }
                }
            }

            _logger.Info(
                $"Software list processing: {addedCount} new entries added, {skippedCount} duplicates skipped (full list has priority).");
            _logger.Info($"After merging software list, there are {uniqueMachines.Count} total unique machines.");

            _logger.Info("--- Starting Process 4: Sorting and Saving Output Files ---");

            // Sort machines by MachineName (case-insensitive)
            _logger.Info("Sorting machines by MachineName...");
            var sortedMachines = uniqueMachines.Values
                .OrderBy(static m => m.MachineName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _logger.Info($"Sorted {sortedMachines.Count} machines alphabetically.");

            // Save XML
            _logger.Info($"Saving merged XML to: {outputXmlPath}");
            XDocument mergedDoc = new(new XElement("Machines",
                sortedMachines.Select(static m => new XElement("Machine",
                    new XElement("MachineName", m.MachineName),
                    new XElement("Description", m.Description)
                ))
            ));
            await Task.Run(() => mergedDoc.Save(outputXmlPath));
            _logger.Info("XML file saved successfully.");

            // Save DAT. If the chosen XML name already ends in .dat, do not let
            // Path.ChangeExtension return the same path (the DAT save would overwrite the
            // merged XML that was just written).
            var datOutputPath = Path.ChangeExtension(outputXmlPath, ".dat");
            if (PathsEqual(datOutputPath, outputXmlPath))
            {
                datOutputPath = outputXmlPath + ".dat";
                _logger.Warning(
                    $"The selected output name is already a .dat file; saving the binary DAT to '{datOutputPath}' so the XML is preserved.");
            }

            _logger.Info($"Saving merged DAT to: {datOutputPath}");
            await SaveMachinesToDatAsync(sortedMachines, datOutputPath);
            _logger.Info("DAT file saved successfully.");

            _logger.Info("\n--- Operation Completed Successfully! ---");
            _logger.Info("Summary:");
            _logger.Info($"  - Total unique machines: {sortedMachines.Count}");
            _logger.Info($"  - Entries from full list: {fullList.Count}");
            _logger.Info($"  - Entries from software list: {softwareList.Count}");
            _logger.Info($"  - New entries added from software list: {addedCount}");
            _logger.Info($"  - Duplicates skipped (full list priority): {skippedCount}");
        }
        catch (Exception ex)
        {
            _logger.Error("A critical error occurred during the process.", ex);
            throw;
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task SaveMachinesToDatAsync(List<MachineInfo> machines, string outputFilePath)
    {
        try
        {
            var binary = await Task.Run(() => MessagePackSerializer.Serialize(machines));
            await File.WriteAllBytesAsync(outputFilePath, binary);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error saving DAT file to {outputFilePath}", ex);
            throw;
        }
    }
}