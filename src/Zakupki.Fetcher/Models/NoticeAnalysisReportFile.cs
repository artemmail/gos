namespace Zakupki.Fetcher.Models;

public sealed record NoticeAnalysisReportFile(
    byte[] Content,
    string ContentType,
    string FileName);
