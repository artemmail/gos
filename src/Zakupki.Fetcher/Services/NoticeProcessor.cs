using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.EF2020;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.Services;

/// <summary>
/// Persists <see cref="NoticeDocument"/> payloads into the application database.
/// The processor performs a best-effort upsert of notices, their versions, procedure windows and attachments.
/// </summary>
public sealed class NoticeProcessor
{
    private readonly ILogger<NoticeProcessor> _logger;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

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
            if (export.Contract is not null)
            {
                await ProcessContractAsync(export.Contract, document, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Document {EntryName} does not contain a supported payload", document.EntryName);
            }

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

        var contractConditions = notification.NotificationInfo?.ContractConditionsInfo;
        var customerRequirements = notification.NotificationInfo?.CustomerRequirementsInfo;
        var serializedNotification = JsonSerializer.Serialize(notification, JsonSerializerOptions);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var existingVersion = await dbContext.NoticeVersions
            .Include(v => v.Notice)
            .FirstOrDefaultAsync(v => v.ExternalId == externalId, cancellationToken);

        var notice = existingVersion?.Notice;
        var isNewNotice = notice is null;
        if (isNewNotice)
        {
            notice = new Notice
            {
                Id = Guid.NewGuid()
            };
        }

        MapNotice(
            notice!,
            document,
            notification,
            commonInfo,
            contractConditions,
            customerRequirements,
            notification.NotificationInfo?.ProcedureInfo,
            serializedNotification,
            externalId);

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

        MapNoticeVersion(version!, document, serializedNotification, notification, externalId, now);
        UpdateProcedureWindow(version!, notification.NotificationInfo?.ProcedureInfo);
        UpdateAttachments(dbContext, version!, notification.AttachmentsInfo?.Items, document.EntryName, now);
        await DeactivateOtherVersionsAsync(dbContext, notice!.Id, version!.Id, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported notice {ExternalId} version {VersionNumber}", externalId, version!.VersionNumber);
    }

    private async Task ProcessContractAsync(ContractExport contract, NoticeDocument document, CancellationToken cancellationToken)
    {
        var externalId = FirstNonEmpty(contract.RegNum, contract.Id, document.EntryName);
        if (string.IsNullOrWhiteSpace(externalId))
        {
            _logger.LogWarning("Unable to determine an external identifier for contract entry {EntryName}", document.EntryName);
            return;
        }

        var serializedContract = JsonSerializer.Serialize(contract, JsonSerializerOptions);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var existingVersion = await dbContext.NoticeVersions
            .Include(v => v.Notice)
            .FirstOrDefaultAsync(v => v.ExternalId == externalId, cancellationToken);

        var notice = existingVersion?.Notice;
        var isNewNotice = notice is null;
        if (isNewNotice)
        {
            notice = new Notice
            {
                Id = Guid.NewGuid()
            };
        }

        MapContractNotice(notice!, document, contract, serializedContract, externalId);

        if (isNewNotice)
        {
            dbContext.Notices.Add(notice!);
        }

        var version = await dbContext.NoticeVersions
            .Include(v => v.Attachments)
                .ThenInclude(a => a.Signatures)
            .Include(v => v.ProcedureWindow)
            .FirstOrDefaultAsync(v => v.NoticeId == notice!.Id && v.VersionNumber == contract.VersionNumber, cancellationToken);

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

        MapContractNoticeVersion(version!, document, serializedContract, contract, externalId, now);

        var contractEntity = await dbContext.Contracts
            .FirstOrDefaultAsync(c => c.ExternalId == externalId, cancellationToken);

        var isNewContract = contractEntity is null;
        if (isNewContract)
        {
            contractEntity = new Contract
            {
                Id = Guid.NewGuid(),
                CreatedAt = now
            };
        }

        MapContractEntity(contractEntity!, document, contract, serializedContract, externalId, now);

        if (isNewContract)
        {
            dbContext.Contracts.Add(contractEntity!);
        }

        await DeactivateOtherVersionsAsync(dbContext, notice!.Id, version!.Id, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Imported contract {ExternalId} version {VersionNumber}", externalId, version!.VersionNumber);
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
        ContractConditionsInfo? contractConditions,
        CustomerRequirementsInfo? customerRequirements,
        ProcedureInfo? procedureInfo,
        string serializedNotification,
        string externalId)
    {
        notice.Region = DetermineRegionCode(notification, document);
        notice.PurchaseNumber = commonInfo.PurchaseNumber ?? externalId;
        notice.PublishDate = commonInfo.PublishDtInEis;
        notice.Href = commonInfo.Href;
        notice.PlacingWayCode = commonInfo.PlacingWay?.Code;
        notice.EtpName = commonInfo.Etp?.Name;
        notice.EtpUrl = commonInfo.Etp?.Url;
        notice.PurchaseObjectInfo = commonInfo.PurchaseObjectInfo;
        notice.MaxPrice = contractConditions?.MaxPriceInfo?.MaxPrice;
        var classifiers = ExtractClassificationInfo(customerRequirements);
        notice.Okpd2Code = classifiers.Okpd2Code;
        notice.Okpd2Name = classifiers.Okpd2Name;
        notice.KvrCode = classifiers.KvrCode;
        notice.KvrName = classifiers.KvrName;
        notice.RawJson = serializedNotification;
        notice.CollectingEnd = procedureInfo?.CollectingInfo?.EndDt;
    }

    private static void MapContractNotice(
        Notice notice,
        NoticeDocument document,
        ContractExport contract,
        string serializedContract,
        string externalId)
    {
        notice.Region = (document.Region);
        notice.PurchaseNumber = contract.Foundation?.FcsOrder?.Order?.NotificationNumber ?? externalId;
        notice.PublishDate = contract.PublishDate;
        notice.Href = contract.Href;
        notice.PlacingWayCode = null;
        notice.EtpName = null;
        notice.EtpUrl = null;
        notice.PurchaseObjectInfo = contract.ContractSubject;
        notice.MaxPrice = contract.PriceInfo?.Price;
        var okpd2 = ExtractContractOkpd2(contract.Products);
        notice.Okpd2Code = okpd2.Code;
        notice.Okpd2Name = okpd2.Name;
        notice.KvrCode = null;
        notice.KvrName = null;
        notice.RawJson = serializedContract;
        notice.CollectingEnd = null;
    }

    private static void MapContractEntity(
        Contract entity,
        NoticeDocument document,
        ContractExport contract,
        string serializedContract,
        string externalId,
        DateTime now)
    {
        entity.Source = document.Source;
        entity.DocumentType = document.DocumentType;
        entity.EntryName = document.EntryName;
        entity.Region = (document.Region);
        entity.Period = document.Period.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        entity.ExternalId = externalId;
        entity.RegNumber = contract.RegNum ?? externalId;
        entity.Number = contract.Number;
        entity.VersionNumber = contract.VersionNumber;
        entity.SchemeVersion = contract.SchemeVersion;
        entity.PurchaseNumber = contract.Foundation?.FcsOrder?.Order?.NotificationNumber ?? externalId;
        entity.LotNumber = contract.Foundation?.FcsOrder?.Order?.LotNumber;
        entity.ContractSubject = contract.ContractSubject;
        entity.Price = contract.PriceInfo?.Price;
        entity.CurrencyCode = contract.PriceInfo?.Currency?.Code;
        entity.CurrencyName = contract.PriceInfo?.Currency?.Name;
        var okpd2 = ExtractContractOkpd2(contract.Products);
        entity.Okpd2Code = okpd2.Code;
        entity.Okpd2Name = okpd2.Name;
        entity.Href = contract.Href;
        entity.PublishDate = contract.PublishDate;
        entity.SignDate = contract.SignDate;
        entity.RawJson = serializedContract;
        entity.UpdatedAt = now;
    }

    private static byte DetermineRegionCode(EpNotificationEf2020 notification, NoticeDocument document)
    {
        var regionFromAddress = ExtractRegionFromFactAddresses(notification);
        if (regionFromAddress > 0)
        {
            return regionFromAddress;
        }

        foreach (var inn in EnumerateInnCandidates(notification))
        {
            var region = ExtractRegionFromInn(inn);
            if (region > 0)
            {
                return region;
            }
        }

        return (document.Region);
    }

    private static byte ExtractRegionFromFactAddresses(EpNotificationEf2020 notification)
    {
        byte? detected = null;

        foreach (var address in EnumerateFactAddresses(notification))
        {
            var region = ExtractRegionFromAddress(address);
            if (region == 0)
            {
                continue;
            }

            if (detected is null)
            {
                detected = region;
            }
            else if (detected.Value != region)
            {
                return 0;
            }
        }

        return detected ?? 0;
    }

    private static IEnumerable<string?> EnumerateInnCandidates(EpNotificationEf2020 notification)
    {
        yield return notification.PurchaseResponsibleInfo?.ResponsibleOrgInfo?.INN;
        yield return notification.PurchaseResponsibleInfo?.SpecializedOrgInfo?.INN;

        var customerRequirements = notification.NotificationInfo?.CustomerRequirementsInfo?.Items;
        if (customerRequirements is not { Count: > 0 })
        {
            yield break;
        }

        foreach (var requirement in customerRequirements)
        {
            var applicationInn = requirement.ApplicationGuarantee?.AccountBudget?.AccountBudgetAdmin?.Inn;
            if (!string.IsNullOrWhiteSpace(applicationInn))
            {
                yield return applicationInn;
            }

            var contractInn = requirement.ContractGuarantee?.AccountBudget?.AccountBudgetAdmin?.Inn;
            if (!string.IsNullOrWhiteSpace(contractInn))
            {
                yield return contractInn;
            }
        }
    }

    private static IEnumerable<string?> EnumerateFactAddresses(EpNotificationEf2020 notification)
    {
        yield return notification.PurchaseResponsibleInfo?.ResponsibleOrgInfo?.FactAddress;
        yield return notification.PurchaseResponsibleInfo?.SpecializedOrgInfo?.FactAddress;
        yield return notification.PurchaseResponsibleInfo?.ResponsibleInfo?.OrgFactAddress;
    }

    private static byte ExtractRegionFromAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return 0;
        }

        var normalizedAddress = NormalizeRegionText(address);
        if (string.IsNullOrEmpty(normalizedAddress))
        {
            return 0;
        }

        byte? detected = null;

        foreach (var keyword in RegionKeywordsToCode)
        {
            if (!normalizedAddress.Contains(keyword.Key, StringComparison.Ordinal))
            {
                continue;
            }

            if (detected is null)
            {
                detected = keyword.Value;
            }
            else if (detected.Value != keyword.Value)
            {
                return 0;
            }
        }

        return detected ?? 0;
    }

    private static byte ExtractRegionFromInn(string? inn)
    {
        if (string.IsNullOrWhiteSpace(inn))
        {
            return 0;
        }

        var trimmed = inn.Trim();
        if (trimmed.Length < 2)
        {
            return 0;
        }

        var digits = trimmed.Where(char.IsDigit).ToArray();
        if (digits.Length < 2)
        {
            return 0;
        }

        return byte.Parse(new string(digits, 0, 2));
    }

    private static string NormalizeRegionText(string source)
    {
        var withoutParentheses = Regex.Replace(source, "\\s*\\([^)]*\\)", " ", RegexOptions.CultureInvariant);
        var lower = withoutParentheses.ToLowerInvariant();
        var normalizedDashes = lower.Replace('—', ' ').Replace('-', ' ');
        var withoutCommonWords = Regex.Replace(
            normalizedDashes,
            "\\b(обл\\.?|область|респ\\.?|республика|г\\.?|город)\\b",
            " ",
            RegexOptions.CultureInvariant);
        var withoutPunctuation = Regex.Replace(withoutCommonWords, "[.,]", " ", RegexOptions.CultureInvariant);
        return Regex.Replace(withoutPunctuation, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
    }


    private static readonly IReadOnlyDictionary<string, byte> RegionKeywordsToCode = CreateRegionKeywordDictionary();

    private static readonly (byte Code, string[] Keywords)[] RegionKeywordSeeds =
    {
        (01, new[] { "адыгея", "адыгейская" }),
        (02, new[] { "башкортостан", "башкирия", "башкирская" }),
        (03, new[] { "бурятия", "бурятская" }),
        (04, new[] { "алтай республика", "горный алтай", "алтай респ" }),
        (05, new[] { "дагестан", "дагестанская" }),
        (06, new[] { "ингушетия", "ингушская" }),
        (07, new[] { "кабардино балкария", "кабардино балкарская" }),
        (08, new[] { "карачаево черкесия", "карачаево черкесская" }),
        (09, new[] { "карелия", "карельская" }),
        (10, new[] { "коми" }),
        (11, new[] { "марий эл" }),
        (12, new[] { "мордовия", "мордовская" }),
        (13, new[] { "мордовия старый", "мордовская старый" }),
        (14, new[] { "саха", "якутия", "саха якутия" }),
        (15, new[] { "северная осетия", "алания", "осетия алания" }),
        (16, new[] { "татарстан", "татарская" }),
        (17, new[] { "тыва", "тува" }),
        (18, new[] { "удмуртия", "удмуртская" }),
        (19, new[] { "хакасия", "хакасская" }),
        (20, new[] { "чечня", "чеченская" }),
        (21, new[] { "чувашия", "чувашская" }),
        (22, new[] { "алтайский край", "алтайский" }),
        (23, new[] { "краснодарский край", "краснодарский" }),
        (24, new[] { "красноярский край", "красноярский" }),
        (25, new[] { "приморский край", "приморский" }),
        (26, new[] { "ставропольский край", "ставропольский" }),
        (27, new[] { "хабаровский край", "хабаровский" }),
        (28, new[] { "амурская" }),
        (29, new[] { "архангельская" }),
        (30, new[] { "астраханская" }),
        (31, new[] { "белгородская" }),
        (32, new[] { "брянская" }),
        (33, new[] { "владимирская" }),
        (34, new[] { "волгоградская" }),
        (35, new[] { "вологодская" }),
        (36, new[] { "воронежская" }),
        (37, new[] { "ивановская" }),
        (38, new[] { "иркутская" }),
        (39, new[] { "калининградская" }),
        (40, new[] { "калужская" }),
        (41, new[] { "камчатский" }),
        (42, new[] { "кемеровская", "кузбасс" }),
        (43, new[] { "кировская" }),
        (44, new[] { "костромская" }),
        (45, new[] { "курганская" }),
        (46, new[] { "курская" }),
        (47, new[] { "ленинградская" }),
        (48, new[] { "липецкая" }),
        (49, new[] { "магаданская" }),
        (50, new[] { "московская" }),
        (51, new[] { "мурманская" }),
        (52, new[] { "нижегородская" }),
        (53, new[] { "новгородская" }),
        (54, new[] { "новосибирская" }),
        (55, new[] { "омская" }),
        (56, new[] { "оренбургская" }),
        (57, new[] { "орловская" }),
        (58, new[] { "пензенская" }),
        (59, new[] { "пермский" }),
        (60, new[] { "псковская" }),
        (61, new[] { "ростовская" }),
        (62, new[] { "рязанская" }),
        (63, new[] { "самарская" }),
        (64, new[] { "саратовская" }),
        (65, new[] { "сахалинская" }),
        (66, new[] { "свердловская" }),
        (67, new[] { "смоленская" }),
        (68, new[] { "тамбовская" }),
        (69, new[] { "тверская" }),
        (70, new[] { "томская" }),
        (71, new[] { "тульская" }),
        (72, new[] { "тюменская" }),
        (73, new[] { "ульяновская" }),
        (74, new[] { "челябинская" }),
        (75, new[] { "забайкальский" }),
        (76, new[] { "ярославская" }),
        (77, new[] { "москва" }),
        (78, new[] { "санкт петербург", "питер" }),
        (79, new[] { "еврейская" }),
        (80, new[] { "забайкальский старый" }),
        (81, new[] { "пермский старый" }),
        (82, new[] { "крым", "крым республика" }),
        (83, new[] { "ненецкий" }),
        (84, new[] { "красноярский старый" }),
        (85, new[] { "иркутская старый" }),
        (86, new[] { "ханты мансийский", "югра", "хмао" }),
        (87, new[] { "чукотский" }),
        (88, new[] { "ямало ненецкий", "янао" }),
        (89, new[] { "ямало ненецкий дубль" }),
        (90, new[] { "запорожская" }),
        (91, new[] { "крым старый" }),
        (92, new[] { "севастополь" }),
        (93, new[] { "херсонская" }),
        (94, new[] { "донецкая", "днр" }),
        (95, new[] { "луганская", "лнр" }),
        (96, new[] { "не используется" }),
        (97, new[] { "москва доп" }),
        (98, new[] { "санкт петербург доп" }),
        (99, new[] { "москва доп" })
    };

    private static IReadOnlyDictionary<string, byte> CreateRegionKeywordDictionary()
    {
        var dictionary = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in RegionKeywordSeeds)
        {
            foreach (var keyword in seed.Keywords)
            {
                AddRegionKeyword(dictionary, keyword, seed.Code);
            }
        }

        return dictionary;
    }

    private static void AddRegionKeyword(Dictionary<string, byte> dictionary, string keyword, byte code)
    {
        var trimmed = keyword?.Trim();
        if (string.IsNullOrEmpty(trimmed) || dictionary.ContainsKey(trimmed))
        {
            return;
        }

        dictionary[trimmed] = code;
    }
    private static string? FormatDocumentRegion(int region)
    {
        if (region <= 0)
        {
            return null;
        }

        return region.ToString("D2", CultureInfo.InvariantCulture);
    }

    private static (string? Okpd2Code, string? Okpd2Name, string? KvrCode, string? KvrName) ExtractClassificationInfo(
        CustomerRequirementsInfo? customerRequirements)
    {
        if (customerRequirements?.Items is not { Count: > 0 })
        {
            return default;
        }

        string? okpd2Code = null;
        string? okpd2Name = null;
        string? kvrCode = null;
        string? kvrName = null;

        foreach (var requirement in customerRequirements.Items)
        {
            var ikzInfo = requirement.InnerContractConditionsInfo?.IkzInfo;
            if (ikzInfo is null)
            {
                continue;
            }

            if (okpd2Code is null && okpd2Name is null)
            {
                (okpd2Code, okpd2Name) = ExtractOkpd2(ikzInfo.Okpd2Info);
            }

            if (kvrCode is null && kvrName is null)
            {
                (kvrCode, kvrName) = ExtractKvr(ikzInfo.KvrInfo);
            }

            if ((okpd2Code is not null || okpd2Name is not null) && (kvrCode is not null || kvrName is not null))
            {
                break;
            }
        }

        return (okpd2Code, okpd2Name, kvrCode, kvrName);
    }

    private static (string? Code, string? Name) ExtractContractOkpd2(ContractProducts? products)
    {
        var firstProduct = products?.Items?.FirstOrDefault(p => p?.Okpd2 is not null);
        if (firstProduct?.Okpd2 is null)
        {
            return default;
        }

        var code = TrimToNull(firstProduct.Okpd2.Code);
        var name = TrimToNull(firstProduct.Okpd2.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = TrimToNull(firstProduct.Name);
        }

        return (code, name);
    }

    private static (string? Code, string? Name) ExtractOkpd2(Okpd2InfoContainer? container)
    {
        if (container is null)
        {
            return default;
        }

        var items = container.Items;
        if (items is not null)
        {
            foreach (var okpd2 in items)
            {
                var code = TrimToNull(okpd2?.Code);
                var name = TrimToNull(okpd2?.Name);
                if (!string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(name))
                {
                    return (code, name);
                }
            }
        }

        var undefined = TrimToNull(container.Undefined);
        if (!string.IsNullOrEmpty(undefined))
        {
            return (undefined, null);
        }

        return default;
    }

    private static (string? Code, string? Name) ExtractKvr(KvrInfoContainer? container)
    {
        if (container?.Items is null)
        {
            return default;
        }

        foreach (var kvr in container.Items)
        {
            var code = TrimToNull(kvr?.Code);
            var name = TrimToNull(kvr?.Name);
            if (!string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(name))
            {
                return (code, name);
            }
        }

        return default;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void MapNoticeVersion(
        NoticeVersion version,
        NoticeDocument document,
        string serializedNotification,
        EpNotificationEf2020 notification,
        string externalId,
        DateTime now)
    {
        version.ExternalId = externalId;
        version.VersionNumber = notification.VersionNumber;
        version.IsActive = true;
        version.VersionReceivedAt = notification.CommonInfo?.PublishDtInEis ?? now;
        version.RawJson = serializedNotification;
        version.Hash = HashUtilities.ComputeSha256Hex(Encoding.UTF8.GetBytes(serializedNotification));
        version.LastSeenAt = now;
        version.SourceFileName = document.EntryName;
    }

    private static void MapContractNoticeVersion(
        NoticeVersion version,
        NoticeDocument document,
        string serializedContract,
        ContractExport contract,
        string externalId,
        DateTime now)
    {
        version.ExternalId = externalId;
        version.VersionNumber = contract.VersionNumber;
        version.IsActive = true;
        version.VersionReceivedAt = contract.PublishDate ?? contract.SignDate ?? now;
        version.RawJson = serializedContract;
        version.Hash = HashUtilities.ComputeSha256Hex(Encoding.UTF8.GetBytes(serializedContract));
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
            entity.ContentHash = HashUtilities.ComputeSha256Hex(Encoding.UTF8.GetBytes(key));

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
}
