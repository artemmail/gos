using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Services;

public sealed class PostalIndexImportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    private readonly RegionDeterminationService _rs;
    private readonly ILogger<PostalIndexImportService> _logger;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;

    public PostalIndexImportService(
        RegionDeterminationService rs,
        ILogger<PostalIndexImportService> logger,
        IDbContextFactory<NoticeDbContext> dbContextFactory)
    {
        _rs = rs;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<PostalIndexImportResult> ImportAsync(string? directory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Не указана директория для импорта", nameof(directory));
        }

        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Директория почтовых индексов '{Directory}' не существует", directory);
            return PostalIndexImportResult.Empty;
        }

        var files = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            _logger.LogInformation("В директории '{Directory}' нет файлов *.json для импорта почтовых индексов", directory);
            return PostalIndexImportResult.Empty;
        }

        var entries = new List<PostalIndexEntry>(files.Length);
        var failed = 0;

        foreach (var file in files)
        {
            PostalIndexPayload? payload;
            try
            {
                await using var stream = File.OpenRead(file);
                payload = await JsonSerializer.DeserializeAsync<PostalIndexPayload>(stream, SerializerOptions, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Не удалось прочитать JSON '{File}'", file);
                continue;
            }

            if (payload is null)
            {
                failed++;
                _logger.LogWarning("Файл '{File}' не содержит данных для импорта почтовых индексов", file);
                continue;
            }

            if (payload.Index <= 0)
            {
                failed++;
                _logger.LogWarning("Файл '{File}' пропущен: отсутствует корректный индекс", file);
                continue;
            }

           



            var entry = new PostalIndexEntry(
                Index: payload.Index,
                OPSName: payload.OPSName?.Trim(),
                OPSType: payload.OPSType?.Trim(),
                OPSSubm: payload.OPSSubm,
                Region: payload.Region?.Trim(),
                RegionId:  _rs.ExtractRegionFromAddress(string.IsNullOrWhiteSpace(payload.Region)?payload.Autonom: payload.Region),
                Autonom: payload.Autonom?.Trim(),
                Area: payload.Area?.Trim(),
                City: payload.City?.Trim(),
                City1: payload.City1?.Trim(),
                ActDate: ParseActDate(payload.ActDate),
                IndexOld: payload.IndexOld?.Trim());

            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            _logger.LogWarning(
                "Импорт почтовых индексов остановлен: ни один файл из директории '{Directory}' не был прочитан успешно",
                directory);
            return new PostalIndexImportResult(0, 0, 0, failed);
        }

        var distinctIndexes = entries
            .Select(e => e.Index)
            .Distinct()
            .ToArray();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.PostalIndices
            .Where(p => distinctIndexes.Contains(p.Index))
            .ToDictionaryAsync(p => p.Index, cancellationToken);

        var processedIndexes = new HashSet<int>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var entry in entries)
        {
            if (!processedIndexes.Add(entry.Index))
            {
                skipped++;
                continue;
            }

            if (existing.TryGetValue(entry.Index, out var stored))
            {
                if (ApplyChanges(stored, entry))
                {
                    updated++;
                }
                else
                {
                    skipped++;
                }

                continue;
            }

            dbContext.PostalIndices.Add(CreateEntity(entry));
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Импорт почтовых индексов завершён. Добавлено: {Created}, обновлено: {Updated}, без изменений: {Skipped}, ошибок: {Failed}",
            created,
            updated,
            skipped,
            failed);

        return new PostalIndexImportResult(created, updated, skipped, failed);
    }

    private static bool ApplyChanges(PostalIndex entity, PostalIndexEntry entry)
    {
        var changed = false;

        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.OPSName != e.OPSName, (p, e) => p.OPSName = e.OPSName);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.OPSType != e.OPSType, (p, e) => p.OPSType = e.OPSType);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.OPSSubm != e.OPSSubm, (p, e) => p.OPSSubm = e.OPSSubm);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.Region != e.Region, (p, e) => p.Region = e.Region);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.RegionId != e.RegionId, (p, e) => p.RegionId = e.RegionId);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.Autonom != e.Autonom, (p, e) => p.Autonom = e.Autonom);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.Area != e.Area, (p, e) => p.Area = e.Area);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.City != e.City, (p, e) => p.City = e.City);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.City1 != e.City1, (p, e) => p.City1 = e.City1);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.ActDate != e.ActDate, (p, e) => p.ActDate = e.ActDate);
        changed |= UpdateIfDifferent(entity, entry, (p, e) => p.IndexOld != e.IndexOld, (p, e) => p.IndexOld = e.IndexOld);

        return changed;
    }

    private static PostalIndex CreateEntity(PostalIndexEntry entry)
    {
        return new PostalIndex
        {
            Index = entry.Index,
            OPSName = entry.OPSName,
            OPSType = entry.OPSType,
            OPSSubm = entry.OPSSubm,
            Region = entry.Region,
            RegionId = entry.RegionId,
            Autonom = entry.Autonom,
            Area = entry.Area,
            City = entry.City,
            City1 = entry.City1,
            ActDate = entry.ActDate,
            IndexOld = entry.IndexOld
        };
    }

    private static bool UpdateIfDifferent(
        PostalIndex entity,
        PostalIndexEntry entry,
        Func<PostalIndex, PostalIndexEntry, bool> predicate,
        Action<PostalIndex, PostalIndexEntry> updater)
    {
        if (!predicate(entity, entry))
        {
            return false;
        }

        updater(entity, entry);
        return true;
    }

    private static DateTime? ParseActDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact.Date;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return parsed.Date;
        }

        return null;
    }

    private sealed record PostalIndexPayload(
        int Index,
        string? OPSName,
        string? OPSType,
        int? OPSSubm,
        string? Region,
        string? Autonom,
        string? Area,
        string? City,
        string? City1,
        string? ActDate,
        string? IndexOld);

    private sealed record PostalIndexEntry(
        int Index,
        string? OPSName,
        string? OPSType,
        int? OPSSubm,
        string? Region,
        byte? RegionId,
        string? Autonom,
        string? Area,
        string? City,
        string? City1,
        DateTime? ActDate,
        string? IndexOld);
}

public readonly record struct PostalIndexImportResult(int Created, int Updated, int Skipped, int Failed)
{
    public static PostalIndexImportResult Empty => new(0, 0, 0, 0);
}
