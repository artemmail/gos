using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public class AttachmentMarkdownService
{
    private static readonly Dictionary<string, string> FormatMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        [".doc"] = "doc",
        [".docx"] = "docx",
        [".pdf"] = "pdf",
        [".html"] = "html",
        [".htm"] = "html"
    };

    private readonly AttachmentConversionOptions _options;
    private readonly ILogger<AttachmentMarkdownService> _logger;

    public AttachmentMarkdownService(
        IOptions<AttachmentConversionOptions> options,
        ILogger<AttachmentMarkdownService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsSupported(NoticeAttachment attachment)
    {
        if (attachment is null)
        {
            throw new ArgumentNullException(nameof(attachment));
        }

        if (string.IsNullOrWhiteSpace(attachment.FileName))
        {
            return false;
        }

        var extension = Path.GetExtension(attachment.FileName);
        return !string.IsNullOrWhiteSpace(extension) && FormatMappings.ContainsKey(extension);
    }

    public async Task<string> ConvertToMarkdownAsync(NoticeAttachment attachment, CancellationToken cancellationToken)
    {
        if (attachment is null)
        {
            throw new ArgumentNullException(nameof(attachment));
        }

        if (attachment.BinaryContent is null || attachment.BinaryContent.Length == 0)
        {
            throw new InvalidOperationException("Attachment does not contain binary content for conversion.");
        }

        if (string.IsNullOrWhiteSpace(attachment.FileName))
        {
            throw new InvalidOperationException("Attachment file name is missing.");
        }

        var extension = Path.GetExtension(attachment.FileName);

        if (string.IsNullOrWhiteSpace(extension) || !FormatMappings.TryGetValue(extension, out var format))
        {
            throw new NotSupportedException($"Conversion for files with extension '{extension}' is not supported.");
        }

        var pandocPath = _options.PandocPath;

        if (string.IsNullOrWhiteSpace(pandocPath))
        {
            throw new InvalidOperationException("PandocPath is not configured. Conversion cannot be performed.");
        }

        var tempDirectory = Path.GetTempPath();
        var tempFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var tempFilePath = Path.Combine(tempDirectory, tempFileName);

        await File.WriteAllBytesAsync(tempFilePath, attachment.BinaryContent, cancellationToken).ConfigureAwait(false);

        try
        {
            var arguments = BuildPandocArguments(tempFilePath, format);
            var workingDirectory = GetWorkingDirectory();

            if (!Directory.Exists(workingDirectory))
            {
                throw new InvalidOperationException($"Указанный рабочий каталог Pandoc '{workingDirectory}' не существует.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pandocPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Pandoc process for attachment {AttachmentId}", attachment.Id);
                throw new InvalidOperationException("Не удалось запустить процесс Pandoc для конвертации файла.", ex);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var markdown = await outputTask.ConfigureAwait(false);
            var errors = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "Pandoc conversion failed for attachment {AttachmentId} with exit code {ExitCode}. Errors: {Errors}",
                    attachment.Id,
                    process.ExitCode,
                    errors);

                throw new InvalidOperationException("Конвертация файла в Markdown завершилась с ошибкой.");
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                _logger.LogWarning(
                    "Pandoc reported warnings during conversion of attachment {AttachmentId}: {Errors}",
                    attachment.Id,
                    errors);
            }

            return markdown;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(cleanupException, "Failed to delete temporary file {TempFilePath}", tempFilePath);
            }
        }
    }

    private static string BuildPandocArguments(string inputFilePath, string format)
    {
        var builder = new StringBuilder();
        builder.Append("--from=").Append(format).Append(' ');
        builder.Append("--to=gfm ");
        builder.Append("--standalone ");
        builder.Append('"').Append(inputFilePath).Append('"');
        return builder.ToString();
    }

    private string GetWorkingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.PandocWorkingDirectory))
        {
            return _options.PandocWorkingDirectory!;
        }

        return Directory.GetCurrentDirectory();
    }
}
