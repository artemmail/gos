using System;

namespace Zakupki.Fetcher.Data.Entities;

public class Contract
{
    public Guid Id { get; set; }

    public string Source { get; set; } = null!;

    public string DocumentType { get; set; } = null!;

    public string EntryName { get; set; } = null!;

    public byte Region { get; set; }

    public string? Period { get; set; }

    public string ExternalId { get; set; } = null!;

    public string? RegNumber { get; set; }

    public string? Number { get; set; }

    public int? VersionNumber { get; set; }

    public string? SchemeVersion { get; set; }

    public string? PurchaseNumber { get; set; }

    public string? LotNumber { get; set; }

    public string? ContractSubject { get; set; }

    public decimal? Price { get; set; }

    public string? CurrencyCode { get; set; }

    public string? CurrencyName { get; set; }

    public string? Okpd2Code { get; set; }

    public string? Okpd2Name { get; set; }

    public string? Href { get; set; }

    public DateTime? PublishDate { get; set; }

    public DateTime? SignDate { get; set; }

    public string? RawJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
