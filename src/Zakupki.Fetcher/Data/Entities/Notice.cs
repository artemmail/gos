using System;
using System.Collections.Generic;

namespace Zakupki.Fetcher.Data.Entities;

public class Notice
{
    public Guid Id { get; set; }

    public string Source { get; set; } = null!;

    public string DocumentType { get; set; } = null!;

    public string? Region { get; set; }

    public string? Period { get; set; }

    public string EntryName { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    public int? VersionNumber { get; set; }

    public string? SchemeVersion { get; set; }

    public string PurchaseNumber { get; set; } = null!;

    public string? DocumentNumber { get; set; }

    public DateTime? PublishDate { get; set; }

    public string? Href { get; set; }

    public string? PlacingWayCode { get; set; }

    public string? PlacingWayName { get; set; }

    public string? EtpCode { get; set; }

    public string? EtpName { get; set; }

    public string? EtpUrl { get; set; }

    public bool? ContractConclusionOnSt83Ch2 { get; set; }

    public string? PurchaseObjectInfo { get; set; }

    public string? Article15FeaturesInfo { get; set; }

    public byte[]? RawXml { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<NoticeVersion> Versions { get; set; } = new List<NoticeVersion>();
}
