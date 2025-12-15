namespace Zakupki.Fetcher.Models.Notices;

public class MissingPurchaseNumbersRequest
{
    public string? Region { get; set; }

    public List<string> PurchaseNumbers { get; set; } = new();
}
