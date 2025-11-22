using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Services;

public sealed class Okpd2ImportService
{
    private readonly ILogger<Okpd2ImportService> _logger;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;

    public Okpd2ImportService(
        ILogger<Okpd2ImportService> logger,
        IDbContextFactory<NoticeDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Okpd2ImportResult> ImportAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Не найден файл для импорта", filePath);
        }

        var entries = LoadEntries(filePath);
        if (entries.Count == 0)
        {
            _logger.LogWarning("В файле '{File}' не найдено строк для импорта", filePath);
            return new Okpd2ImportResult(0, 0, 0);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var distinctCodes = entries.Select(e => e.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = await dbContext.Okpd2Codes
            .Where(c => distinctCodes.Contains(c.Code))
            .ToListAsync(cancellationToken);

        var existingByCode = existing.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        foreach (var entry in entries)
        {
            if (existingByCode.TryGetValue(entry.Code, out var stored))
            {
                if (!string.Equals(stored.Name, entry.Name, StringComparison.Ordinal))
                {
                    stored.Name = entry.Name;
                    stored.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    skipped++;
                }

                continue;
            }

            var entity = new Okpd2Code
            {
                Code = entry.Code,
                Name = entry.Name,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Okpd2Codes.Add(entity);
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Импорт ОКПД2 завершён. Добавлено: {Created}, обновлено: {Updated}, без изменений: {Skipped}",
            created,
            updated,
            skipped);

        return new Okpd2ImportResult(created, updated, skipped);
    }

    private static List<Okpd2Entry> LoadEntries(string filePath)
    {
        using var document = SpreadsheetDocument.Open(filePath, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("Некорректный формат книги: отсутствует WorkbookPart");
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidDataException("В книге отсутствуют листы");

        if (sheet.Id == null)
        {
            throw new InvalidDataException("Лист не содержит идентификатор");
        }

        if (workbookPart.GetPartById(sheet.Id) is not WorksheetPart worksheetPart)
        {
            throw new InvalidDataException("Не удалось открыть лист книги");
        }

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var rows = worksheetPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToList()
            ?? throw new InvalidDataException("В листе отсутствуют данные");

        var headerRow = rows.FirstOrDefault(row =>
            string.Equals(GetCellValue(row, "B", sharedStrings), "код", StringComparison.OrdinalIgnoreCase));

        if (headerRow == null)
        {
            throw new InvalidDataException("Файл не прошёл проверку: во втором столбце не найден заголовок 'Код'");
        }

        var entries = new List<Okpd2Entry>();
        foreach (var row in rows.Where(r => r.RowIndex.HasValue && r.RowIndex.Value > headerRow.RowIndex!.Value))
        {
            var code = GetCellValue(row, "B", sharedStrings);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var name = GetCellValue(row, "C", sharedStrings);
            entries.Add(new Okpd2Entry(code.Trim(), (name ?? string.Empty).Trim()));
        }

        return entries;
    }

    private static string? GetCellValue(Row row, string columnName, SharedStringTable? sharedStrings)
    {
        var cell = row.Elements<Cell>()
            .FirstOrDefault(c => string.Equals(GetColumnName(c.CellReference?.Value), columnName, StringComparison.OrdinalIgnoreCase));

        if (cell == null)
        {
            return null;
        }

        var value = cell.CellValue?.InnerText;
        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            if (int.TryParse(value, out var sharedStringIndex) && sharedStrings != null)
            {
                var sharedItem = sharedStrings.ElementAtOrDefault(sharedStringIndex);
                return sharedItem?.InnerText;
            }
        }

        return value;
    }

    private static string? GetColumnName(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var ch in cellReference)
        {
            if (char.IsLetter(ch))
            {
                builder.Append(ch);
            }
            else
            {
                break;
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    private sealed record Okpd2Entry(string Code, string Name);
}

public readonly record struct Okpd2ImportResult(int Created, int Updated, int Skipped);
