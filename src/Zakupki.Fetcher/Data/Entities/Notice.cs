using System;
using System.Collections.Generic;
using Microsoft.Data.SqlTypes;

namespace Zakupki.Fetcher.Data.Entities;

public class Notice
{
    public Guid Id { get; set; }

    public byte Region { get; set; }

    public string PurchaseNumber { get; set; } = null!;

    public DateTime? PublishDate { get; set; }

    public string? Href { get; set; }

    public string? PlacingWayCode { get; set; }


    public string? EtpName { get; set; }

    public string? EtpUrl { get; set; }

    public string? PurchaseObjectInfo { get; set; }

    public decimal? MaxPrice { get; set; }

    public string? Okpd2Code { get; set; }

    public string? Okpd2Name { get; set; }

    public string? KvrCode { get; set; }

    public string? KvrName { get; set; }

    public string? RawJson { get; set; }

    public DateTime? CollectingEnd { get; set; }

    public SqlVector<float>? Vector { get; set; } = null!;

    public ICollection<NoticeVersion> Versions { get; set; } = new List<NoticeVersion>();

    public ICollection<NoticeAnalysis> Analyses { get; set; } = new List<NoticeAnalysis>();

    public ICollection<FavoriteNotice> Favorites { get; set; } = new List<FavoriteNotice>();
}
