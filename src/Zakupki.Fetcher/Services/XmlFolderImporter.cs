using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.EF2020;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.Services;

public sealed class XmlFolderImporter
{
    private readonly ILogger<XmlFolderImporter> _logger;
    private readonly NoticeProcessor _processor;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;

    public XmlFolderImporter(
        ILogger<XmlFolderImporter> logger,
        NoticeProcessor processor,
        IDbContextFactory<NoticeDbContext> dbContextFactory)
    {
        _logger = logger;
        _processor = processor;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> ImportAsync(string? directory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("XML import directory '{Directory}' does not exist", directory);
            return false;
        }

        var existingPurchaseNumbers = await LoadExistingPurchaseNumbersAsync(cancellationToken);

        var files = Directory
            .EnumerateFiles(directory, "*.xml", SearchOption.AllDirectories)
            .Where(path =>
            {
                var parentDir = Path.GetFileName(Path.GetDirectoryName(path));
                return parentDir != null && !existingPurchaseNumbers.Contains(parentDir);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            _logger.LogInformation("XML import directory '{Directory}' does not contain any XML files", directory);
            return true;
        }

        var processed = 0;
        var duplicates = 0;
        var errors = 0;
        var skippedExistingPurchases = 0;
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            /*
            if (TryExtractPurchaseNumber(file, out var purchaseNumber) &&
                existingPurchaseNumbers.Contains(purchaseNumber))
            {
                skippedExistingPurchases++;
                _logger.LogDebug(
                    "Skipping '{File}' because purchase number {PurchaseNumber} already exists in the database",
                    file,
                    purchaseNumber);
                continue;
            }*/

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(file, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Failed to load XML file '{File}'", file);
                continue;
            }

            var hash = HashUtilities.ComputeSha256Hex(content);

            if (!seenHashes.Add(hash))
            {
                duplicates++;
                _logger.LogDebug("Skipping '{File}' because an identical file has already been scheduled for import", file);
                continue;
            }

            if (await IsDuplicateAsync(hash, cancellationToken))
            {
                duplicates++;
                _logger.LogInformation("Skipping '{File}' because an identical notice version already exists in the database", file);
                continue;
            }

            Export exportModel;
            try
            {
                using var stream = new MemoryStream(content);
                exportModel = ZakupkiLoader.LoadFromStream(stream);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Failed to deserialize XML file '{File}'", file);
                continue;
            }

            var metadata = ExtractMetadata(directory, file);
            var document = new NoticeDocument(
                source: "LocalXml",
                documentType: metadata.DocumentType,
                region: metadata.Region,
                period: metadata.Period,
                entryName: metadata.EntryName,
                content: content,
                exportModel: exportModel);

            try
            {
                await _processor.ProcessAsync(document, cancellationToken);
                processed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Failed to persist XML entry '{Entry}' from '{Directory}'", document.EntryName, directory);
            }
        }

        _logger.LogInformation(
            "Imported {Processed} XML file(s) from '{Directory}'. Skipped {Skipped} duplicates, {Existing} existing purchases, encountered {Errors} error(s)",
            processed,
            directory,
            duplicates,
            skippedExistingPurchases,
            errors);

        return true;
    }

    private async Task<bool> IsDuplicateAsync(string hash, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.NoticeVersions
            .AsNoTracking()
            .AnyAsync(v => v.Hash == hash, cancellationToken);
    }

    private static (string DocumentType, int Region, DateTime Period, string EntryName) ExtractMetadata(string rootDirectory, string file)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, file);
        if (relativePath == ".")
        {
            relativePath = Path.GetFileName(file);
        }

        var fileName = Path.GetFileNameWithoutExtension(file);
        var documentType = "unknown";
        var period = DateTime.UtcNow.Date;

        var parts = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            documentType = parts[1];
            if (DateTime.TryParseExact(parts[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                period = parsed;
            }
        }

        return (documentType, 0, period, relativePath.Replace(Path.DirectorySeparatorChar, '/'));
    }

    private async Task<HashSet<string>> LoadExistingPurchaseNumbersAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var purchaseNumbers = await dbContext.Notices
            .AsNoTracking()
            .Select(n => n.PurchaseNumber)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(purchaseNumbers, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryExtractPurchaseNumber(string file, out string purchaseNumber)
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        var parts = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (part.Length >= 5 && part.All(char.IsDigit))
            {
                purchaseNumber = part;
                return true;
            }
        }

        purchaseNumber = string.Empty;
        return false;
    }
}
