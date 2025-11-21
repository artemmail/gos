using System;
using Zakupki.EF2020;

namespace Zakupki.Fetcher.Models;

public sealed class NoticeDocument
{
    public NoticeDocument(
        string source,
        string documentType,
        byte region,
        DateTime period,
        string entryName,
        byte[] content,
        Export? exportModel)
    {
        Source = source;
        DocumentType = documentType;
        Region = region;
        Period = period;
        EntryName = entryName;
        Content = content;
        ExportModel = exportModel;
    }

    public string Source { get; }

    public string DocumentType { get; }

    public byte Region { get; }

    public DateTime Period { get; }

    public string EntryName { get; }

    public byte[] Content { get; }

    public Export? ExportModel { get; }
}
