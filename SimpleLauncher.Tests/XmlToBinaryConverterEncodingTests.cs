using System.Xml;
using MessagePack;
using Xunit;
using XmlToBinaryConverter.Models;
using XmlToBinaryConverter.Services;

namespace SimpleLauncher.Tests;

/// <summary>
///     Regression tests for the XML/binary converter: the XML declaration must match
///     the bytes written (UTF-8), not the StringWriter's UTF-16 declaration.
/// </summary>
public class XmlToBinaryConverterEncodingTests
{
    [Fact]
    public async Task ConvertBinaryToXmlAsync_WritesDeclaredUtf8Xml()
    {
        var directory = Directory.CreateTempSubdirectory("sol-xmlc-");
        try
        {
            var history = new History
            {
                Version = "1",
                Date = "2026-09-16",
                Entries = []
            };
            var inputPath = Path.Combine(directory.FullName, "history.dat");
            var outputPath = Path.Combine(directory.FullName, "history.xml");
            await File.WriteAllBytesAsync(inputPath, MessagePackSerializer.Serialize(history));

            await new ConverterService().ConvertBinaryToXmlAsync(inputPath, outputPath,
                new Progress<string>(static _ => { }));

            var xml = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("encoding=\"utf-8\"", xml, StringComparison.OrdinalIgnoreCase);

            // Before the fix the file declared utf-16 while being UTF-8 without a BOM,
            // so standard readers threw "There is no Unicode byte order mark".
            var document = new XmlDocument();
            document.Load(outputPath);
            Assert.NotNull(document.DocumentElement);
            Assert.Equal("history", document.DocumentElement!.Name);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
