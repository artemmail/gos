using System;
using System.Collections.Generic;

namespace Zakupki.Fetcher.Data.Entities;

public class MosNotice
{
    public Guid Id { get; set; }

    public int ExternalId { get; set; }

    public string RegisterNumber { get; set; } = null!;

    public string? RegistrationNumber { get; set; }

    public string? Name { get; set; }

    public DateTimeOffset? RegistrationDate { get; set; }

    public DateTimeOffset? SummingUpDate { get; set; }

    public DateTimeOffset? EndFillingDate { get; set; }

    public DateTimeOffset? PlanDate { get; set; }

    public double? InitialSum { get; set; }

    public int? StateId { get; set; }

    public string? StateName { get; set; }

    public string? FederalLawName { get; set; }

    public string? CustomerInn { get; set; }

    public string? CustomerName { get; set; }

    public string? RawJson { get; set; }

    public DateTime InsertedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public ICollection<MosNoticeAttachment> Attachments { get; set; } = new List<MosNoticeAttachment>();
}
