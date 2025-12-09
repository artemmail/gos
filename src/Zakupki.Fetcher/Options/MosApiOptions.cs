namespace Zakupki.Fetcher.Options;

public class MosApiOptions
{
    public const string SectionName = "MosApi";

    public string? BaseUrl { get; set; }

    public string? Token { get; set; }

    public int SyncIntervalMinutes { get; set; } = 60;

    public int LookbackDays { get; set; } = 30;

    public int PageSize { get; set; } = 200;
}
