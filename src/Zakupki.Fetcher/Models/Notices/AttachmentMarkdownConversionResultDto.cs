namespace Zakupki.Fetcher.Models.Notices;

public record AttachmentMarkdownConversionResultDto(
    int Total,
    int Converted,
    int MissingContent,
    int Unsupported,
    int Failed);
