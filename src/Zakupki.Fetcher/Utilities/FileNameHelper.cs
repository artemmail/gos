using System.IO;
using System.Text.RegularExpressions;

namespace Zakupki.Fetcher.Utilities;

internal static class FileNameHelper
{
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "file";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new char[fileName.Length];
        var length = 0;
        foreach (var ch in fileName)
        {
            builder[length++] = invalidChars.Contains(ch) ? '_' : ch;
        }

        var sanitized = new string(builder, 0, length);
        sanitized = WhitespaceRegex.Replace(sanitized, " ").Trim();
        if (sanitized.Length > 180)
        {
            sanitized = sanitized[..180];
        }

        return sanitized.Length == 0 ? "file" : sanitized;
    }

    public static string SanitizeDirectoryName(string directoryName)
    {
        return SanitizeFileName(directoryName);
    }

    public static string StripTabs(string value)
    {
        return value
            .Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}
