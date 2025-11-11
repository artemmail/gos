using System.Collections.Generic;

namespace Zakupki.Fetcher.Options;

public class ZakupkiOptions
{
    public const string DefaultSubsystem = "PRIZ";

    public static IReadOnlyList<int> DefaultRegions { get; } = new List<int> { 77 };

    public static IReadOnlyList<string> DefaultDocumentTypes { get; } = new List<string>
    {
        "epNotificationEF2020"
    };

    public string? Token { get; set; }

    public string? Subsystem { get; set; } = DefaultSubsystem;

    public int Days { get; set; } = 1;

    public List<int> Regions { get; set; } = new();

    public List<string> DocumentTypes { get; set; } = new();

    public string OutputDirectory { get; set; } = "out";

    public bool DownloadAttachments { get; set; } = false;

    public bool FetchByPurchaseNumber { get; set; } = false;

    public List<string> PurchaseNumbers { get; set; } = new();

    public int IntervalMinutes { get; set; } = 0;

    public int MaxArchiveMegabytes { get; set; } = 100;

    public string? XmlImportDirectory { get; set; }
}
