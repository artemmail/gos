using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.Services;

public class NoticeProcessor
{
    private readonly ILogger<NoticeProcessor> _logger;
    private readonly ZakupkiOptions _options;

    public NoticeProcessor(IOptions<ZakupkiOptions> options, ILogger<NoticeProcessor> logger)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task ProcessAsync(NoticeDocument document, CancellationToken cancellationToken)
    {
        var baseDir = string.IsNullOrWhiteSpace(_options.OutputDirectory) ? "out" : _options.OutputDirectory!;
        var export = document.ExportModel;
        var notification = export?.AnyNotification;
        var purchaseNumber = notification?.CommonInfo?.PurchaseNumber;
        var safePurchase = !string.IsNullOrWhiteSpace(purchaseNumber)
            ? FileNameHelper.SanitizeDirectoryName(purchaseNumber!)
            : "unknown";

        var targetDir = Path.Combine(baseDir, safePurchase);
        Directory.CreateDirectory(targetDir);

        var entryName = FileNameHelper.SanitizeFileName(document.EntryName);
        var fileName = $"notice_{document.DocumentType}_{document.Period:yyyy-MM-dd}_{entryName}";
        var filePath = Path.Combine(targetDir, fileName);
        await File.WriteAllBytesAsync(filePath, document.Content, cancellationToken);

        await AppendManifestAsync(targetDir, document, export, cancellationToken);

        if (_options.DownloadAttachments && notification?.AttachmentsInfo?.Items is { Count: > 0 } attachments)
        {
            foreach (var attachment in attachments)
            {
                _logger.LogInformation(
                    "Attachment listed for purchase {Purchase}: {FileName} ({Description})",
                    purchaseNumber ?? "unknown",
                    attachment.FileName,
                    attachment.DocDescription);
            }
        }
    }

    private static async Task AppendManifestAsync(string targetDir, NoticeDocument document, Zakupki.EF2020.Export? export, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(targetDir, "manifest.tsv");
        var builder = new StringBuilder();

        if (!File.Exists(manifestPath))
        {
            builder.AppendLine("DocumentType\tRegion\tDate\tSource\tEntry\tPurchaseNumber\tPublishDate\tHref\tAttachments");
        }

        var notification = export?.AnyNotification;
        var purchaseNumber = notification?.CommonInfo?.PurchaseNumber ?? string.Empty;
        var publishDate = notification?.CommonInfo?.PublishDtInEis.ToString("yyyy-MM-ddTHH:mm:ss") ?? string.Empty;
        var href = notification?.CommonInfo?.Href ?? string.Empty;
        var attachments = notification?.AttachmentsInfo?.Items?
            .Where(a => !string.IsNullOrWhiteSpace(a.FileName))
            .Select(a => FileNameHelper.StripTabs(a.FileName))
            .ToArray() ?? Array.Empty<string>();

        builder
            .Append(document.DocumentType).Append('\t')
            .Append(document.Region).Append('\t')
            .Append(document.Period.ToString("yyyy-MM-dd")).Append('\t')
            .Append(document.Source).Append('\t')
            .Append(FileNameHelper.StripTabs(document.EntryName)).Append('\t')
            .Append(FileNameHelper.StripTabs(purchaseNumber)).Append('\t')
            .Append(publishDate).Append('\t')
            .Append(href).Append('\t')
            .Append(string.Join(',', attachments))
            .AppendLine();

        await File.AppendAllTextAsync(manifestPath, builder.ToString(), Encoding.UTF8, cancellationToken);
    }
}
