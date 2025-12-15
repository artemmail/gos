namespace Zakupki.Fetcher.Models.Notices;

public sealed class UndocumentedAuctionDto
{
    public string? name { get; set; }

    public string? federalLawName { get; set; }

    public UndocumentedAuctionCustomerDto? customer { get; set; }
}

public sealed class UndocumentedAuctionCustomerDto
{
    public string? inn { get; set; }

    public string? name { get; set; }
}
