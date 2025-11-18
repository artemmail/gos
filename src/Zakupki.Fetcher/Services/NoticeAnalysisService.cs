using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeAnalysisService
{
    private const int MaxAttachmentCharacters = 4000;
    private const int MaxTotalAttachmentCharacters = 16000;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly AttachmentDownloadService _attachmentDownloadService;
    private readonly AttachmentContentExtractor _attachmentContentExtractor;
    private readonly AttachmentMarkdownService _attachmentMarkdownService;
    private readonly OpenAiOptions _options;
    private readonly ILogger<NoticeAnalysisService> _logger;

    public NoticeAnalysisService(
        HttpClient httpClient,
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        AttachmentDownloadService attachmentDownloadService,
        AttachmentContentExtractor attachmentContentExtractor,
        AttachmentMarkdownService attachmentMarkdownService,
        IOptions<OpenAiOptions> options,
        ILogger<NoticeAnalysisService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _attachmentDownloadService = attachmentDownloadService ?? throw new ArgumentNullException(nameof(attachmentDownloadService));
        _attachmentContentExtractor = attachmentContentExtractor ?? throw new ArgumentNullException(nameof(attachmentContentExtractor));
        _attachmentMarkdownService = attachmentMarkdownService ?? throw new ArgumentNullException(nameof(attachmentMarkdownService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.BaseAddress is null &&
            !string.IsNullOrWhiteSpace(_options.BaseUrl) &&
            Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            if (!baseUri.AbsoluteUri.EndsWith('/'))
            {
                baseUri = new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);
            }

            _httpClient.BaseAddress = baseUri;
        }
    }

    public async Task<NoticeAnalysisResponse> AnalyzeAsync(
        Guid noticeId,
        string userId,
        bool force,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new NoticeAnalysisException(
                "API-ключ OpenAI не настроен. Обратитесь к администратору системы.",
                true);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await context.Users
            .Include(u => u.Regions)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NoticeAnalysisException("Пользователь не найден.", true);

        if (string.IsNullOrWhiteSpace(user.CompanyInfo))
        {
            throw new NoticeAnalysisException("Заполните описание компании в профиле пользователя.", true);
        }

        if (user.Regions.Count == 0)
        {
            throw new NoticeAnalysisException("Укажите хотя бы один регион в профиле пользователя.", true);
        }

        var notice = await context.Notices
            .Include(n => n.Versions.Where(v => v.IsActive))
                .ThenInclude(v => v.Attachments)
            .Include(n => n.Versions.Where(v => v.IsActive))
                .ThenInclude(v => v.ProcedureWindow)
            .FirstOrDefaultAsync(n => n.Id == noticeId, cancellationToken)
            ?? throw new NoticeAnalysisException("Закупка не найдена.", true);

        var activeVersion = notice.Versions.FirstOrDefault(v => v.IsActive)
            ?? throw new NoticeAnalysisException("Для закупки отсутствует активная версия.", true);

        var attachments = activeVersion.Attachments
            .OrderBy(a => a.FileName)
            .ToList();

        if (attachments.Count == 0)
        {
            throw new NoticeAnalysisException(
                "Сначала скачайте вложения для закупки и конвертируйте их в Markdown.",
                true);
        }

        await EnsureAttachmentsDownloadedAsync(context, attachments, cancellationToken);
        await EnsureAttachmentsConvertedToMarkdownAsync(context, attachments, cancellationToken);

        if (!attachments.Any(a => !string.IsNullOrWhiteSpace(a.MarkdownContent)))
        {
            throw new NoticeAnalysisException(
                "Не удалось подготовить вложения для анализа. Попробуйте выполнить конвертацию вручную и повторите попытку.",
                true);
        }

        var existingAnalysis = await context.NoticeAnalyses
            .FirstOrDefaultAsync(a => a.NoticeId == noticeId && a.UserId == userId, cancellationToken);

        if (existingAnalysis is not null && !force)
        {
            if (existingAnalysis.Status == NoticeAnalysisStatus.InProgress)
            {
                return ToResponse(existingAnalysis);
            }

            if (existingAnalysis.Status == NoticeAnalysisStatus.Completed &&
                !string.IsNullOrWhiteSpace(existingAnalysis.Result))
            {
                return ToResponse(existingAnalysis);
            }
        }

        var now = DateTime.UtcNow;

        var analysis = existingAnalysis ?? new NoticeAnalysis
        {
            Id = Guid.NewGuid(),
            NoticeId = noticeId,
            UserId = userId,
            CreatedAt = now
        };

        analysis.Status = NoticeAnalysisStatus.InProgress;
        analysis.Result = null;
        analysis.Error = null;
        analysis.CompletedAt = null;
        analysis.UpdatedAt = now;

        if (existingAnalysis is null)
        {
            context.NoticeAnalyses.Add(analysis);
        }

        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var regions = user.Regions
                .Select(r => UserCompanyService.ResolveRegionName(r.Region))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!.Trim())
                .Where(r => r.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var prompt = BuildPrompt(
                notice,
                user.CompanyInfo!,
                regions,
                attachments,
                activeVersion.ProcedureWindow);

            var answer = await RequestAnalysisAsync(prompt, cancellationToken);

            analysis.Status = NoticeAnalysisStatus.Completed;
            analysis.Result = answer;
            analysis.CompletedAt = DateTime.UtcNow;
            analysis.UpdatedAt = analysis.CompletedAt.Value;

            await context.SaveChangesAsync(cancellationToken);

            return ToResponse(analysis, prompt);
        }
        catch (NoticeAnalysisException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze notice {NoticeId}", noticeId);

            analysis.Status = NoticeAnalysisStatus.Failed;
            analysis.Error = ex.Message;
            analysis.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            throw new NoticeAnalysisException(
                "Не удалось выполнить анализ закупки. Попробуйте повторить позже.",
                false,
                ex);
        }
    }

    public async Task<NoticeAnalysisResponse> GetStatusAsync(
        Guid noticeId,
        string userId,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var analysis = await context.NoticeAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.NoticeId == noticeId && a.UserId == userId, cancellationToken);

        if (analysis is null)
        {
            return new NoticeAnalysisResponse(
                noticeId,
                NoticeAnalysisStatus.NotStarted,
                false,
                null,
                null,
                DateTime.UtcNow,
                null,
                null);
        }

        return ToResponse(analysis);
    }

    private async Task EnsureAttachmentsDownloadedAsync(
        NoticeDbContext context,
        List<NoticeAttachment> attachments,
        CancellationToken cancellationToken)
    {
        var updated = false;

        foreach (var attachment in attachments)
        {
            if (attachment.BinaryContent is not null && attachment.BinaryContent.Length > 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(attachment.Url))
            {
                continue;
            }

            try
            {
                var content = await _attachmentDownloadService.DownloadAsync(
                    attachment.Url!,
                    cancellationToken: cancellationToken);

                UpdateAttachmentContent(attachment, content, null);
                updated = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to download attachment {AttachmentId} while preparing notice {NoticeId} for analysis",
                    attachment.Id,
                    attachment.NoticeVersion.NoticeId);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to process attachment {AttachmentId} content while preparing notice {NoticeId} for analysis",
                    attachment.Id,
                    attachment.NoticeVersion.NoticeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while downloading attachment {AttachmentId} for notice {NoticeId}",
                    attachment.Id,
                    attachment.NoticeVersion.NoticeId);
            }
        }

        if (updated)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureAttachmentsConvertedToMarkdownAsync(
        NoticeDbContext context,
        List<NoticeAttachment> attachments,
        CancellationToken cancellationToken)
    {
        var updated = false;

        foreach (var attachment in attachments)
        {
            if (attachment.BinaryContent is null || attachment.BinaryContent.Length == 0)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(attachment.MarkdownContent))
            {
                continue;
            }

            var attachmentForConversion = PrepareAttachmentForConversion(attachment);

            if (!_attachmentMarkdownService.IsSupported(attachmentForConversion))
            {
                continue;
            }

            try
            {
                var markdown = await _attachmentMarkdownService.ConvertToMarkdownAsync(attachmentForConversion, cancellationToken);

                if (string.IsNullOrWhiteSpace(markdown))
                {
                    attachment.MarkdownContent = null;
                    _logger.LogWarning(
                        "Markdown conversion produced empty result for attachment {AttachmentId} while preparing notice {NoticeId} for analysis",
                        attachment.Id,
                        attachment.NoticeVersion.NoticeId);
                    continue;
                }

                attachment.MarkdownContent = markdown;
                updated = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to convert attachment {AttachmentId} to Markdown while preparing notice {NoticeId} for analysis",
                    attachment.Id,
                    attachment.NoticeVersion.NoticeId);
            }
        }

        if (updated)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private NoticeAttachment PrepareAttachmentForConversion(NoticeAttachment attachment)
    {
        if (attachment.BinaryContent is null || attachment.BinaryContent.Length == 0)
        {
            throw new InvalidOperationException("Attachment does not contain binary content for conversion.");
        }

        var processed = _attachmentContentExtractor.Process(attachment, attachment.BinaryContent);

        if (ReferenceEquals(processed.Content, attachment.BinaryContent) && string.IsNullOrWhiteSpace(processed.FileNameOverride))
        {
            return attachment;
        }

        return new NoticeAttachment
        {
            Id = attachment.Id,
            FileName = processed.FileNameOverride ?? attachment.FileName,
            BinaryContent = processed.Content
        };
    }

    private static void UpdateAttachmentContent(NoticeAttachment attachment, byte[] content, string? newFileName)
    {
        attachment.BinaryContent = content;
        attachment.FileSize = content.LongLength;
        attachment.ContentHash = HashUtilities.ComputeSha256Hex(content);
        attachment.LastSeenAt = DateTime.UtcNow;
        attachment.MarkdownContent = null;

        if (!string.IsNullOrWhiteSpace(newFileName))
        {
            attachment.FileName = newFileName;
        }
    }

    private async Task<string> RequestAnalysisAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        // Поведение модели — через instructions
        var instructions = @"
Ты — эксперт по государственным закупкам.
Используя данные о закупке, профиле компании и вложениях, оцени, подходит ли закупка компании.
Ответ должен быть на русском языке и учитывать соответствие предмета закупки, условий и регионов.

Сформируй ответ в следующем виде:
1. Итог: подходит ли закупка (подходит / не подходит / требуется уточнение).
2. Обоснование: ключевые факторы соответствия или несоответствия.
3. Рекомендации: что делать компании дальше.

Ответ должен быть кратким, но информативным.
".Trim();

        var requestBody = new
        {
            model = _options.Model,
            instructions,
            input = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = prompt
                        }
                    }
                }
            },
            temperature = 0.2,
            max_output_tokens = 800,
            // response_format больше не используется в Responses API,
            // текст вернётся по умолчанию
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, SerializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        if (!string.IsNullOrWhiteSpace(_options.Organization))
        {
            request.Headers.Add("OpenAI-Organization", _options.Organization);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenAI returned error {StatusCode}: {Content}",
                response.StatusCode,
                responseContent);

            throw new Exception("Сервис анализа вернул ошибку.");
        }

        var answer = ExtractAnswer(responseContent);

        if (string.IsNullOrWhiteSpace(answer))
        {
            _logger.LogWarning("OpenAI response does not contain a text answer. Raw: {Content}", responseContent);
            throw new Exception("Сервис анализа не вернул текстовый ответ.");
        }

        return answer.Trim();
    }

    private static string ExtractAnswer(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // 1. Новый Responses API: "output" -> [ { "content": [ { "text": \"...\" } или { \"text\": { \"value\": \"...\" } } ] } ]
        if (root.TryGetProperty("output", out var outputElement) &&
            outputElement.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();

            foreach (var item in outputElement.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var contentArray) ||
                    contentArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (!contentItem.TryGetProperty("text", out var textElement))
                    {
                        continue;
                    }

                    // text: "..."
                    if (textElement.ValueKind == JsonValueKind.String)
                    {
                        var text = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            builder.Append(text);
                        }

                        continue;
                    }

                    // text: { "value": "..." }
                    if (textElement.ValueKind == JsonValueKind.Object &&
                        textElement.TryGetProperty("value", out var valueElement) &&
                        valueElement.ValueKind == JsonValueKind.String)
                    {
                        var text = valueElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            builder.Append(text);
                        }
                    }
                }
            }

            if (builder.Length > 0)
            {
                return builder.ToString();
            }
        }

        // 2. Совместимость с классическими Chat / Completions
        if (root.TryGetProperty("choices", out var choicesElement) &&
            choicesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choicesElement.EnumerateArray())
            {
                // text (старый completions)
                if (choice.TryGetProperty("text", out var legacyTextElement) &&
                    legacyTextElement.ValueKind == JsonValueKind.String)
                {
                    var text = legacyTextElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }

                // message -> content (chat)
                if (choice.TryGetProperty("message", out var messageElement) &&
                    messageElement.TryGetProperty("content", out var contentElement))
                {
                    // строка
                    if (contentElement.ValueKind == JsonValueKind.String)
                    {
                        var text = contentElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }

                    // массив контента (ChatML-формат)
                    if (contentElement.ValueKind == JsonValueKind.Array)
                    {
                        var builder = new StringBuilder();
                        foreach (var part in contentElement.EnumerateArray())
                        {
                            if (!part.TryGetProperty("text", out var textElement) ||
                                textElement.ValueKind != JsonValueKind.String)
                            {
                                continue;
                            }

                            var text = textElement.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                builder.Append(text);
                            }
                        }

                        if (builder.Length > 0)
                        {
                            return builder.ToString();
                        }
                    }
                }
            }
        }

        return string.Empty;
    }

    private static string BuildPrompt(
        Notice notice,
        string companyInfo,
        IReadOnlyCollection<string> regions,
        IReadOnlyCollection<NoticeAttachment> attachments,
        ProcedureWindow? procedureWindow)
    {
        var builder = new StringBuilder();

        // Здесь ТОЛЬКО данные, без инструкций к модели

        builder.AppendLine("Профиль компании:");
        builder.AppendLine(companyInfo.Trim());
        builder.AppendLine();

        if (regions.Count > 0)
        {
            builder.AppendLine("Целевые регионы компании: " + string.Join(", ", regions));
            builder.AppendLine();
        }

        builder.AppendLine("Основная информация о закупке:");
        builder.AppendLine($"Номер закупки: {notice.PurchaseNumber}");
        builder.AppendLine($"Наименование извещения: {notice.EntryName}");
        builder.AppendLine($"Предмет закупки: {notice.PurchaseObjectInfo ?? "не указан"}");

        var noticeRegion = UserCompanyService.ResolveRegionName(notice.Region) ?? notice.Region ?? "не указан";
        builder.AppendLine($"Регион: {noticeRegion}");
        builder.AppendLine($"Период публикации: {notice.Period ?? "не указан"}");
        builder.AppendLine($"Площадка: {notice.EtpName ?? "не указана"}");
        builder.AppendLine($"Способ размещения: {notice.PlacingWayName ?? "не указан"}");

        if (notice.PublishDate is not null)
        {
            builder.AppendLine($"Дата публикации: {notice.PublishDate:yyyy-MM-dd HH:mm}");
        }

        if (notice.MaxPrice is not null)
        {
            var currency = string.IsNullOrWhiteSpace(notice.MaxPriceCurrencyCode)
                ? notice.MaxPriceCurrencyName
                : notice.MaxPriceCurrencyCode;

            var priceString = notice.MaxPrice.Value.ToString("N2", CultureInfo.InvariantCulture);
            builder.AppendLine($"Начальная цена: {priceString} {currency}".Trim());
        }

        if (!string.IsNullOrWhiteSpace(notice.Okpd2Code) ||
            !string.IsNullOrWhiteSpace(notice.Okpd2Name))
        {
            builder.AppendLine($"ОКПД2: {notice.Okpd2Code ?? "-"} — {notice.Okpd2Name ?? "не указано"}");
        }

        if (!string.IsNullOrWhiteSpace(notice.KvrCode) ||
            !string.IsNullOrWhiteSpace(notice.KvrName))
        {
            builder.AppendLine($"КВР: {notice.KvrCode ?? "-"} — {notice.KvrName ?? "не указано"}");
        }

        if (notice.CollectingEnd is not null)
        {
            builder.AppendLine(
                $"Окончание подачи заявок: {notice.CollectingEnd:yyyy-MM-dd HH:mm}");
        }

        if (procedureWindow is not null)
        {
            if (!string.IsNullOrWhiteSpace(procedureWindow.SubmissionProcedureDateRaw))
            {
                builder.AppendLine($"Дата процедуры подачи: {procedureWindow.SubmissionProcedureDateRaw}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Фрагменты вложений (Markdown):");

        var totalCharacters = 0;
        var index = 0;

        foreach (var attachment in OrderAttachmentsForPrompt(attachments))
        {
            index++;

            if (string.IsNullOrWhiteSpace(attachment.MarkdownContent))
            {
                continue;
            }

            builder.AppendLine($"### Вложение {index}: {attachment.FileName}");

            var sanitized = attachment.MarkdownContent.Replace("\r", string.Empty);
            var maxLength = Math.Min(MaxAttachmentCharacters, MaxTotalAttachmentCharacters - totalCharacters);

            if (maxLength <= 0)
            {
                builder.AppendLine("[Дальнейшее содержимое опущено из-за ограничения размера.]");
                break;
            }

            var excerpt = sanitized.Length > maxLength
                ? sanitized[..maxLength] + " …"
                : sanitized;

            builder.AppendLine(excerpt.Trim());
            builder.AppendLine();

            totalCharacters += Math.Min(sanitized.Length, maxLength);

            if (totalCharacters >= MaxTotalAttachmentCharacters)
            {
                builder.AppendLine("[Остальные вложения не включены, чтобы сохранить размер запроса.]");
                break;
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<NoticeAttachment> OrderAttachmentsForPrompt(IEnumerable<NoticeAttachment> attachments)
    {
        return attachments
            .OrderBy(GetAttachmentPriority)
            .ThenBy(a => a.FileName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetAttachmentPriority(NoticeAttachment attachment)
    {
        var fileName = attachment.FileName ?? string.Empty;
        var lowerName = fileName.ToLowerInvariant();
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;

        if (ContainsExtension(lowerName, extension, ".docx"))
        {
            return 0;
        }

        if (ContainsExtension(lowerName, extension, ".doc"))
        {
            return 1;
        }

        if (ContainsExtension(lowerName, extension, ".pdf"))
        {
            return 2;
        }

        return 3;
    }

    private static bool ContainsExtension(string lowerName, string extension, string expected)
    {
        return extension == expected || lowerName.Contains(expected, StringComparison.Ordinal);
    }

    private static NoticeAnalysisResponse ToResponse(NoticeAnalysis analysis, string? prompt = null)
    {
        var hasAnswer = analysis.Status == NoticeAnalysisStatus.Completed &&
                        !string.IsNullOrWhiteSpace(analysis.Result);

        return new NoticeAnalysisResponse(
            analysis.NoticeId,
            analysis.Status,
            hasAnswer,
            analysis.Result,
            analysis.Error,
            analysis.UpdatedAt,
            analysis.CompletedAt,
            prompt);
    }
}
