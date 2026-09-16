using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using MessagePack;
using XmlToBinaryConverter.Models;

namespace XmlToBinaryConverter.Services;

/// <summary>
///     Provides XML to binary and binary to XML conversion functionality.
/// </summary>
public class ConverterService
{
    /// <summary>
    ///     Converts an XML file to binary MessagePack format asynchronously.
    /// </summary>
    /// <param name="inputPath">The path to the input XML file.</param>
    /// <param name="outputPath">The path to save the output binary file.</param>
    /// <param name="progress">Progress reporter for status updates.</param>
    public async Task ConvertXmlToBinaryAsync(string inputPath, string outputPath, IProgress<string> progress)
    {
        try
        {
            progress.Report("Reading XML file...");

            // Read XML content as string
            var xmlContent = await File.ReadAllTextAsync(inputPath);

            progress.Report("Parsing XML content...");

            // Deserialize XML to objects
            var serializer = new XmlSerializer(typeof(History));
            History? history;
            using (var reader = new StringReader(xmlContent))
            using (var xmlReader = XmlReader.Create(reader, new XmlReaderSettings
                   {
                       DtdProcessing = DtdProcessing.Prohibit,
                       XmlResolver = null
                   }))
            {
                try
                {
                    history = serializer.Deserialize(xmlReader) as History;
                }
                catch (InvalidOperationException ex) when (ex.InnerException != null)
                {
                    throw new InvalidOperationException(
                        $"Failed to deserialize XML content. Inner exception: {ex.InnerException.Message}", ex);
                }
            }

            if (history == null) throw new InvalidOperationException("Failed to deserialize XML content.");

            progress.Report("Serializing to binary format...");

            // Serialize to MessagePack
            var binaryData = MessagePackSerializer.Serialize(history);

            progress.Report("Saving binary file...");

            // Save to the output file
            await File.WriteAllBytesAsync(outputPath, binaryData);

            progress.Report("Conversion completed successfully!");
            Log.Information("XML to binary conversion completed: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during XML to binary conversion from {InputPath}", inputPath);
            progress.Report($"Error during conversion: {ex.Message}");
            // Rethrow the exception so the caller (ViewModel) can handle it further (e.g., logging)
            throw;
        }
    }


    /// <summary>
    ///     Converts a binary MessagePack file to XML format asynchronously.
    /// </summary>
    /// <param name="inputPath">The path to the input binary file.</param>
    /// <param name="outputPath">The path to save the output XML file.</param>
    /// <param name="progress">Progress reporter for status updates.</param>
    public async Task ConvertBinaryToXmlAsync(string inputPath, string outputPath, IProgress<string> progress)
    {
        try
        {
            progress.Report("Reading binary file...");

            // Read binary data
            var binaryData = await File.ReadAllBytesAsync(inputPath);

            progress.Report("Deserializing from binary format...");

            // Deserialize from MessagePack
            var history = MessagePackSerializer.Deserialize<History>(binaryData);

            progress.Report("Saving XML file...");

            // Serialize objects back to XML. A StringWriter reports UTF-16 and makes the
            // serializer declare encoding="utf-16" while the file itself is written as
            // UTF-8; serialize through an XmlWriter over a UTF-8 stream instead so the
            // declaration matches the bytes (standard readers reject the mismatch).
            var serializer = new XmlSerializer(typeof(History));
            await using (var stream = new MemoryStream())
            {
                var settings = new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = true,
                    Async = true
                };
                await using (var xmlWriter = XmlWriter.Create(stream, settings))
                {
                    serializer.Serialize(xmlWriter, history);
                    await xmlWriter.FlushAsync();
                }

                await File.WriteAllBytesAsync(outputPath, stream.ToArray());
            }

            progress.Report("Conversion completed successfully!");
            Log.Information("Binary to XML conversion completed: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during binary to XML conversion from {InputPath}", inputPath);
            progress.Report($"Error during conversion: {ex.Message}");
            // Rethrow the exception so the caller (ViewModel) can handle it further (e.g., logging)
            throw;
        }
    }
}