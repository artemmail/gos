using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Services;

public class AttachmentContentExtractor
{
    private static readonly string[] ArchiveExtensions = [".zip", ".rar"];

    private readonly ILogger<AttachmentContentExtractor> _logger;

    public AttachmentContentExtractor(ILogger<AttachmentContentExtractor> logger)
    {
        _logger = logger;
    }

    public AttachmentContentExtractionResult Process(NoticeAttachment attachment, byte[] content)
    {
        if (attachment == null)
        {
            throw new ArgumentNullException(nameof(attachment));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (content.Length == 0)
        {
            return new AttachmentContentExtractionResult(content, null);
        }

        var extension = Path.GetExtension(attachment.FileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !ArchiveExtensions.Contains(extension))
        {
            return new AttachmentContentExtractionResult(content, null);
        }

        var desiredFileName = Path.GetFileNameWithoutExtension(attachment.FileName);
        if (string.IsNullOrWhiteSpace(desiredFileName))
        {
            desiredFileName = attachment.FileName.Substring(0, attachment.FileName.Length - extension.Length);
        }

        if (string.IsNullOrWhiteSpace(desiredFileName))
        {
            desiredFileName = attachment.FileName;
        }

        try
        {
            var extractedContent = extension switch
            {
                ".zip" => TryExtractFromZip(content, desiredFileName),
                ".rar" => TryExtractFromRar(content, desiredFileName),
                _ => null
            };

            if (extractedContent == null)
            {
                _logger.LogWarning(
                    "Failed to extract archive for attachment {AttachmentId} (file: {FileName})", 
                    attachment.Id,
                    attachment.FileName);
                return new AttachmentContentExtractionResult(content, null);
            }

            return new AttachmentContentExtractionResult(extractedContent, desiredFileName);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(
                ex,
                "Archive for attachment {AttachmentId} is invalid (file: {FileName})",
                attachment.Id,
                attachment.FileName);
            return new AttachmentContentExtractionResult(content, null);
        }
       /* catch (RarException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to process RAR archive for attachment {AttachmentId} (file: {FileName})",
                attachment.Id,
                attachment.FileName);
            return new AttachmentContentExtractionResult(content, null);
        }*/
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unexpected error while extracting archive for attachment {AttachmentId} (file: {FileName})",
                attachment.Id,
                attachment.FileName);
            return new AttachmentContentExtractionResult(content, null);
        }
    }

    private static byte[]? TryExtractFromZip(byte[] content, string desiredFileName)
    {
        using var stream = new MemoryStream(content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, Encoding.GetEncoding(866));
        var entries = archive.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .ToList();

        if (entries.Count == 0)
        {
            return null;
        }

        var entry = FindMatchingEntry(entries, desiredFileName);
        if (entry == null)
        {
            return null;
        }

        using var entryStream = entry.Open();
        using var memoryStream = new MemoryStream();
        entryStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static ZipArchiveEntry? FindMatchingEntry(IEnumerable<ZipArchiveEntry> entries, string desiredFileName)
    {
        var match = entries.FirstOrDefault(e => string.Equals(e.Name, desiredFileName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return match;
        }

        return entries
            .OrderByDescending(e => e.Length)
            .FirstOrDefault();
    }

    private static byte[]? TryExtractFromRar(byte[] content, string desiredFileName)
    {
        using var stream = new MemoryStream(content);
        using var archive = RarArchive.Open(stream);
        var entries = archive.Entries
            .Where(e => !e.IsDirectory)
            .ToList();

        if (entries.Count == 0)
        {
            return null;
        }

        var entry = FindMatchingEntry(entries, desiredFileName);
        if (entry == null)
        {
            return null;
        }

        using var memoryStream = new MemoryStream();
        using var entryStream = entry.OpenEntryStream();
        entryStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static IArchiveEntry? FindMatchingEntry(IEnumerable<IArchiveEntry> entries, string desiredFileName)
    {
        var match = entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.Key), desiredFileName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return match;
        }

        return entries
            .OrderByDescending(e => e.Size)
            .FirstOrDefault();
    }
}

public record AttachmentContentExtractionResult(byte[] Content, string? FileNameOverride);
