using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zakupki.EF2020;

namespace Zakupki.XmlToJson;

internal static class Program
{
    private sealed record ManifestData(
        IReadOnlyList<Dictionary<string, string?>> Meta,
        IReadOnlyList<ManifestFile> Files);

    private sealed record ManifestDocument(string RelativePath, ManifestData? Data, string? ErrorMessage);

    private sealed record ManifestFile(string? Ordinal, string? Source, string? Url, string? SavedAs, string? ContentType, string? Bytes);

    private sealed record NoticeDocument(string RelativePath, Export? Data, string? ErrorMessage);

    private sealed record PurchaseDirectory(string RelativePath, ManifestDocument Manifest, IReadOnlyList<NoticeDocument> Notices);

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var inputDirectory = args.Length > 0
            ? args[0]
            : Path.Combine(Environment.CurrentDirectory, "out");

        if (!Directory.Exists(inputDirectory))
        {
            Console.Error.WriteLine($"Input directory '{inputDirectory}' does not exist.");
            Console.Error.WriteLine("Pass the directory created by eis_fetch_all.py as the first argument.");
            return 1;
        }

        var outputPath = args.Length > 1 ? args[1] : Path.Combine(inputDirectory, "zakupki.json");

        var xmlFiles = Directory
            .EnumerateFiles(inputDirectory, "*.xml", SearchOption.AllDirectories)
            .ToArray();

        if (xmlFiles.Length == 0)
        {
            Console.Error.WriteLine($"No XML files were found inside '{inputDirectory}'.");
            return 1;
        }

        var totalXml = 0;
        var successCount = 0;
        var grouped = xmlFiles
            .GroupBy(file => Path.GetDirectoryName(file) ?? inputDirectory, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var purchases = new List<PurchaseDirectory>(grouped.Length);

        foreach (var group in grouped)
        {
            var directoryPath = group.Key;
            var relativeDirectory = Path.GetRelativePath(inputDirectory, directoryPath);
            if (relativeDirectory == ".")
            {
                relativeDirectory = string.Empty;
            }

            var manifest = ReadManifest(inputDirectory, directoryPath, relativeDirectory);
            if (manifest.ErrorMessage is { } manifestError)
            {
                Console.Error.WriteLine($"Manifest issue in '{manifest.RelativePath}': {manifestError}");
            }

            var notices = new List<NoticeDocument>();

            foreach (var file in group.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                totalXml++;
                var relativePath = Path.GetRelativePath(inputDirectory, file);

                try
                {
                    var export = ZakupkiLoader.LoadFromFile(file);
                    notices.Add(new NoticeDocument(relativePath, export, null));
                    successCount++;
                }
                catch (Exception ex)
                {
                    notices.Add(new NoticeDocument(relativePath, null, ex.Message));
                    Console.Error.WriteLine($"Failed to parse '{relativePath}': {ex.Message}");
                }
            }

            purchases.Add(new PurchaseDirectory(relativeDirectory, manifest, notices));
        }

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? inputDirectory);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(purchases, jsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Console.WriteLine($"Processed {totalXml} XML files across {purchases.Count} purchase folders, successfully parsed {successCount}.");
        Console.WriteLine($"JSON output saved to '{outputPath}'.");

        return successCount == totalXml ? 0 : 2;
    }

    private static ManifestDocument ReadManifest(string inputDirectory, string directoryPath, string relativeDirectory)
    {
        var manifestRelativePath = Path.Combine(relativeDirectory, "manifest.tsv");
        var manifestFullPath = Path.Combine(directoryPath, "manifest.tsv");

        if (!File.Exists(manifestFullPath))
        {
            return new ManifestDocument(manifestRelativePath, null, "Manifest file was not found.");
        }

        try
        {
            var data = ParseManifest(manifestFullPath);
            return new ManifestDocument(Path.GetRelativePath(inputDirectory, manifestFullPath), data, null);
        }
        catch (Exception ex)
        {
            return new ManifestDocument(Path.GetRelativePath(inputDirectory, manifestFullPath), null, ex.Message);
        }
    }

    private static ManifestData ParseManifest(string manifestPath)
    {
        var lines = File.ReadAllLines(manifestPath, Encoding.UTF8);
        var metaRows = new List<Dictionary<string, string?>>(capacity: 1);
        var fileRows = new List<ManifestFile>();

        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                if (trimmed.Equals("# meta", StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    if (index >= lines.Length)
                    {
                        break;
                    }

                    var header = SplitTsvLine(lines[index]);
                    index++;

                    while (index < lines.Length)
                    {
                        var rowLine = lines[index];
                        if (rowLine.TrimStart().StartsWith("#", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(rowLine))
                        {
                            if (rowLine.TrimStart().StartsWith("#", StringComparison.Ordinal))
                            {
                                break;
                            }

                            index++;
                            continue;
                        }

                        metaRows.Add(ParseMetaRow(header, rowLine));
                        index++;
                    }

                    continue;
                }

                if (trimmed.Equals("# files", StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    if (index >= lines.Length)
                    {
                        break;
                    }

                    var header = SplitTsvLine(lines[index]);
                    index++;

                    while (index < lines.Length)
                    {
                        var rowLine = lines[index];
                        if (rowLine.TrimStart().StartsWith("#", StringComparison.Ordinal))
                        {
                            break;
                        }

                        if (!string.IsNullOrWhiteSpace(rowLine))
                        {
                            fileRows.Add(ParseFileRow(header, rowLine));
                        }

                        index++;
                    }

                    continue;
                }
            }

            index++;
        }

        return new ManifestData(metaRows, fileRows);
    }

    private static Dictionary<string, string?> ParseMetaRow(string[] header, string line)
    {
        var values = SplitTsvLine(line);
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < header.Length; i++)
        {
            var key = header[i];
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            var value = i < values.Length ? values[i] : null;
            row[key] = string.IsNullOrEmpty(value) ? null : value;
        }

        return row;
    }

    private static ManifestFile ParseFileRow(string[] header, string line)
    {
        var values = SplitTsvLine(line);

        return new ManifestFile(
            GetField(header, values, "ordinal"),
            GetField(header, values, "source"),
            GetField(header, values, "url"),
            GetField(header, values, "saved_as"),
            GetField(header, values, "content_type"),
            GetField(header, values, "bytes"));
    }

    private static string? GetField(string[] header, string[] values, string fieldName)
    {
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i].Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return i < values.Length && !string.IsNullOrEmpty(values[i]) ? values[i] : null;
            }
        }

        return null;
    }

    private static string[] SplitTsvLine(string line)
    {
        return line.Split('\t');
    }
}
