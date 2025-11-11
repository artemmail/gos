using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.EF2020;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Services;

/// <summary>
/// Persists <see cref="NoticeDocument"/> payloads into the application database.
/// The processor performs a best-effort upsert of notices, their versions, procedure windows and attachments.
/// </summary>
public sealed class NoticeProcessor
{
    private readonly ILogger<NoticeProcessor> _logger;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;

    public NoticeProcessor(
        ILogger<NoticeProcessor> logger,
        IDbContextFactory<NoticeDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public async Task ProcessAsync(NoticeDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        Export export;
        try
        {
            export = document.ExportModel ?? LoadExport(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize XML for entry {EntryName}", document.EntryName);
            return;
        }

        var notification = export.AnyNotification;
        if (notification is null)
        {
            _logger.LogWarning("Document {EntryName} does not contain a supported notification payload", document.EntryName);
            return;
        }

        var commonInfo = notification.CommonInfo;
        if (commonInfo is null)
        {
            _logger.LogWarning("Notification {EntryName} is missing the commonInfo block", document.EntryName);
            return;
        }

        var externalId = FirstNonEmpty(notification.ExternalId, notification.Id, commonInfo.PurchaseNumber, document.EntryName);
        if (string.IsNullOrWhiteSpace(externalId))
        {
            _logger.LogWarning("Unable to determine an external identifier for entry {EntryName}", document.EntryName);
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var notice = await dbContext.Notices.FirstOrDefaultAsync(n => n.ExternalId == externalId, cancellationToken);
        var isNewNotice = notice is null;
        if (isNewNotice)
        {
            notice = new Notice
            {
                Id = Guid.NewGuid(),
                CreatedAt = now
            };
        }

        MapNotice(notice!, document, notification, commonInfo, externalId, now);

        if (isNewNotice)
        {
            dbContext.Notices.Add(notice!);
        }

        var version = await dbContext.NoticeVersions
            .Include(v => v.Attachments)
                .ThenInclude(a => a.Signatures)
            .Include(v => v.ProcedureWindow)
            .FirstOrDefaultAsync(v => v.NoticeId == notice!.Id && v.VersionNumber == notification.VersionNumber, cancellationToken);

        var isNewVersion = version is null;
        if (isNewVersion)
        {
            version = new NoticeVersion
            {
                Id = Guid.NewGuid(),
                NoticeId = notice!.Id,
                Notice = notice!,
                InsertedAt = now
            };
            notice!.Versions.Add(version);
            dbContext.NoticeVersions.Add(version);
        }

        MapNoticeVersion(version!, document, notification, externalId, now);
        UpdateProcedureWindow(version!, notification.NotificationInfo?.ProcedureInfo);
        UpdateAttachments(dbContext, version!, notification.AttachmentsInfo?.Items, document.EntryName, now);
        await DeactivateOtherVersionsAsync(dbContext, notice!.Id, version!.Id, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported notice {ExternalId} version {VersionNumber}", externalId, version!.VersionNumber);
    }

    private static Export LoadExport(NoticeDocument document)
    {
        using var stream = new MemoryStream(document.Content);
        return ZakupkiLoader.LoadFromStream(stream);
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        return candidates.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
    }

    private static void MapNotice(
        Notice notice,
        NoticeDocument document,
        EpNotificationEf2020 notification,
        CommonInfo commonInfo,
        string externalId,
        DateTime now)
    {
        notice.Source = document.Source;
        notice.DocumentType = document.DocumentType;
        notice.Region = document.Region.ToString(CultureInfo.InvariantCulture);
        notice.Period = document.Period.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        notice.EntryName = document.EntryName;
        notice.ExternalId = externalId;
        notice.VersionNumber = notification.VersionNumber;
        notice.SchemeVersion = notification.SchemeVersion;
        notice.PurchaseNumber = commonInfo.PurchaseNumber ?? externalId;
        notice.DocumentNumber = commonInfo.DocNumber;
        notice.PublishDate = commonInfo.PublishDtInEis;
        notice.Href = commonInfo.Href;
        notice.PlacingWayCode = commonInfo.PlacingWay?.Code;
        notice.PlacingWayName = commonInfo.PlacingWay?.Name;
        notice.EtpCode = commonInfo.Etp?.Code;
        notice.EtpName = commonInfo.Etp?.Name;
        notice.EtpUrl = commonInfo.Etp?.Url;
        notice.ContractConclusionOnSt83Ch2 = commonInfo.ContractConclusionOnSt83Ch2;
        notice.PurchaseObjectInfo = commonInfo.PurchaseObjectInfo;
        notice.Article15FeaturesInfo = commonInfo.Article15FeaturesInfo;
        notice.RawXml = document.Content;
        notice.UpdatedAt = now;
    }

    private static void MapNoticeVersion(
        NoticeVersion version,
        NoticeDocument document,
        EpNotificationEf2020 notification,
        string externalId,
        DateTime now)
    {
        version.ExternalId = externalId;
        version.VersionNumber = notification.VersionNumber;
        version.IsActive = true;
        version.VersionReceivedAt = notification.CommonInfo?.PublishDtInEis ?? now;
        version.RawXml = document.Content;
        version.Hash = ComputeHash(document.Content);
        version.LastSeenAt = now;
        version.SourceFileName = document.EntryName;
    }

    private static void UpdateProcedureWindow(NoticeVersion version, ProcedureInfo? procedureInfo)
    {
        if (procedureInfo is null)
        {
            return;
        }

        var window = version.ProcedureWindow;
        if (window is null)
        {
            window = new ProcedureWindow
            {
                Id = Guid.NewGuid(),
                NoticeVersionId = version.Id
            };
            version.ProcedureWindow = window;
        }

        window.CollectingStart = procedureInfo.CollectingInfo?.StartDt;
        window.CollectingEnd = procedureInfo.CollectingInfo?.EndDt;
        window.BiddingDateRaw = procedureInfo.BiddingDateRaw;
        window.SummarizingDateRaw = procedureInfo.SummarizingDateRaw;
        window.FirstPartsDateRaw = procedureInfo.FirstPartsDateRaw;
        window.SubmissionProcedureDateRaw = procedureInfo.SubmissionProcedureDateRaw;
        window.SecondPartsDateRaw = procedureInfo.SecondPartsDateRaw;
    }

    private static void UpdateAttachments(
        NoticeDbContext dbContext,
        NoticeVersion version,
        IList<AttachmentInfo>? attachments,
        string sourceFileName,
        DateTime now)
    {
        attachments ??= new List<AttachmentInfo>();

        var incoming = attachments
            .Where(a => !string.IsNullOrWhiteSpace(a.PublishedContentId))
            .ToList();

        var existing = version.Attachments.ToDictionary(a => a.PublishedContentId, StringComparer.OrdinalIgnoreCase);
        var incomingIds = new HashSet<string>(incoming.Select(a => a.PublishedContentId!), StringComparer.OrdinalIgnoreCase);

        foreach (var attachment in version.Attachments.Where(a => !incomingIds.Contains(a.PublishedContentId)).ToList())
        {
            dbContext.AttachmentSignatures.RemoveRange(attachment.Signatures);
            version.Attachments.Remove(attachment);
            dbContext.NoticeAttachments.Remove(attachment);
        }

        foreach (var item in incoming)
        {
            var key = item.PublishedContentId!;
            if (!existing.TryGetValue(key, out var entity))
            {
                entity = new NoticeAttachment
                {
                    Id = Guid.NewGuid(),
                    NoticeVersionId = version.Id,
                    PublishedContentId = key,
                    InsertedAt = now,
                    LastSeenAt = now
                };
                version.Attachments.Add(entity);
                existing[key] = entity;
            }

            entity.FileName = string.IsNullOrWhiteSpace(item.FileName) ? key : item.FileName!;
            entity.FileSize = item.FileSize;
            entity.Description = item.DocDescription;
            entity.DocumentDate = item.DocDate == default ? null : item.DocDate;
            entity.DocumentKindCode = item.DocKindInfo?.Code;
            entity.DocumentKindName = item.DocKindInfo?.Name;
            entity.Url = item.Url;
            entity.SourceFileName = sourceFileName;
            entity.LastSeenAt = now;
            entity.ContentHash = ComputeHash(Encoding.UTF8.GetBytes(key));

            UpdateAttachmentSignatures(dbContext, entity, item.CryptoSigns);
        }
    }

    private static void UpdateAttachmentSignatures(
        NoticeDbContext dbContext,
        NoticeAttachment attachment,
        IList<Signature>? signatures)
    {
        signatures ??= new List<Signature>();
        var normalized = signatures
            .Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => (Type: s.Type ?? string.Empty, Value: s.Value!.Trim()))
            .ToList();

        var signatureKeys = new HashSet<(string Type, string Value)>(normalized);

        foreach (var existing in attachment.Signatures.ToList())
        {
            var key = (existing.SignatureType, existing.SignatureValue);
            if (!signatureKeys.Contains(key))
            {
                attachment.Signatures.Remove(existing);
                dbContext.AttachmentSignatures.Remove(existing);
            }
        }

        foreach (var (type, value) in normalized)
        {
            var signature = attachment.Signatures.FirstOrDefault(s => s.SignatureType == type && s.SignatureValue == value);
            if (signature is null)
            {
                signature = new AttachmentSignature
                {
                    Id = Guid.NewGuid(),
                    AttachmentId = attachment.Id,
                    SignatureType = type,
                    SignatureValue = value
                };
                attachment.Signatures.Add(signature);
            }
        }
    }

    private static async Task DeactivateOtherVersionsAsync(
        NoticeDbContext dbContext,
        Guid noticeId,
        Guid activeVersionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var otherVersions = await dbContext.NoticeVersions
            .Where(v => v.NoticeId == noticeId && v.Id != activeVersionId)
            .ToListAsync(cancellationToken);

        foreach (var version in otherVersions)
        {
            if (version.IsActive)
            {
                version.IsActive = false;
            }

            version.LastSeenAt = now;
        }
    }

    private static string ComputeHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash);
    }
}
