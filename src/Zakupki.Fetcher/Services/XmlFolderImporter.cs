using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        var files = Directory
            .EnumerateFiles(directory, "*.xml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            _logger.LogInformation("XML import directory '{Directory}' does not contain any XML files", directory);
            return true;
        }

        var processed = 0;
        var skipped = 0;
        var errors = 0;
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllBytesAsync(file, cancellationToken);
                var hash = HashUtilities.ComputeSha256Hex(content);

                if (!seenHashes.Add(hash))
                {
                    skipped++;
                    _logger.LogDebug("Skipping '{File}' because an identical file has already been scheduled for import", file);
                    continue;
                }

                if (await IsDuplicateAsync(hash, cancellationToken))
                {
                    skipped++;
                    _logger.LogInformation("Skipping '{File}' because an identical notice version already exists in the database", file);
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
                    exportModel: null);

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
                _logger.LogError(ex, "Failed to import XML file '{File}'", file);
            }
        }

        _logger.LogInformation(
            "Imported {Processed} XML file(s) from '{Directory}'. Skipped {Skipped} duplicates, encountered {Errors} error(s)",
            processed,
            directory,
            skipped,
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
}
