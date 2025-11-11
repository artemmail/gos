using System;

namespace Zakupki.Fetcher.Data.Entities;

public class ProcedureWindow
{
    public Guid Id { get; set; }

    public Guid NoticeVersionId { get; set; }

    public DateTime? CollectingStart { get; set; }

    public DateTime? CollectingEnd { get; set; }

    public string? BiddingDateRaw { get; set; }

    public string? SummarizingDateRaw { get; set; }

    public string? FirstPartsDateRaw { get; set; }

    public string? SubmissionProcedureDateRaw { get; set; }

    public string? SecondPartsDateRaw { get; set; }

    public NoticeVersion NoticeVersion { get; set; } = null!;
}
