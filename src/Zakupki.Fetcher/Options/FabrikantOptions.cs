namespace Zakupki.Fetcher.Options;

public class FabrikantOptions
{
    public const string SectionName = "Fabrikant";

    public string BaseUrl { get; set; } = "https://www.fabrikant.ru";

    public string Rsc { get; set; } = "ibhqu";

    public int PageSize { get; set; } = 40;

    public int MaxPages { get; set; } = 5;

    public int SyncIntervalMinutes { get; set; } = 60;

    public int LookbackDays { get; set; } = 30;

    public byte DefaultRegion { get; set; } = 77;

    public string[] SectionIds { get; set; } =
    {
        "3", "17", "24", "25", "26", "21", "27", "28", "30", "31", "2"
    };

    public string[] Statuses { get; set; } = { "1" };
}
