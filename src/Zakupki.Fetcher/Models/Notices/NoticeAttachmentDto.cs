namespace Zakupki.Fetcher.Models.Notices;

public record NoticeAttachmentDto(
    Guid Id,
    string PublishedContentId,
    string FileName,
    long? FileSize,
    string? Description,
    DateTime? DocumentDate,
    string? DocumentKindCode,
    string? DocumentKindName,
    string? Url,
    string? SourceFileName,
    DateTime InsertedAt,
    DateTime LastSeenAt,
    bool HasBinaryContent,
    bool HasMarkdownContent);
