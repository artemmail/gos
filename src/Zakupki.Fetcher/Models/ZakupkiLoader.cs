using System.IO;
using System.Xml.Serialization;

namespace Zakupki.EF2020;

/// <summary>
/// Helper methods for loading Zakupki XML notifications into strongly typed models.
/// </summary>
public static class ZakupkiLoader
{
    /// <summary>
    /// Reads a notification file from disk and deserialises it into the <see cref="Export"/> root model.
    /// </summary>
    public static Export LoadFromFile(string path)
    {
        using var fs = File.OpenRead(path);
        return LoadFromStream(fs);
    }

    /// <summary>
    /// Deserialises the provided stream into the <see cref="Export"/> model.
    /// </summary>
    public static Export LoadFromStream(Stream stream)
    {
        var serializer = new XmlSerializer(typeof(Export));

        // Note: XML files may include comments and insignificant whitespace, which the serializer handles automatically.
        return (Export)serializer.Deserialize(stream)!;
    }
}
