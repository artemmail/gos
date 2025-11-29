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
using Zakupki.EF2020;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeAnalysisService
{
    private const int MaxAttachmentCharacters = 40000;
    private const int MaxTotalAttachmentCharacters = 160000;
    private const string StructuredResponseInstructions = @"Ты — эксперт по госзакупкам в РФ и аналитик по рентабельности контрактов.

Тебе передают:
1) Профиль компании (чем занимается, какие регионы целевые, какие товары/услуги производит).
2) Описание конкретной закупки (номер, предмет, регион, НМЦК, площадка, сроки и т.д.).
3) Выжимки из вложений (части ТЗ, проекта контракта, требований к участникам и т.п.).

Твоя задача — оценить эту закупку для данной компании по трём критериям:
- profitability (рентабельность),
- attractiveness (выгодность / общая привлекательность),
- risk (риски).

Для каждого из этих трёх критериев ты обязан выдать:
- score — числовую оценку (от 0.0 до 1.0, где 0 — плохо, 1 — максимально хорошо),
- shortComment — 1–2 предложения краткого комментария на русском языке,
- detailedComment — подробный комментарий на русском языке в формате Markdown (можно использовать **жирный текст**, списки, переносы строк).

Также ты обязан выдать:
- decisionScore — интегральный балл (0.0–1.0), который рассчитывается независимо от частных оценок критериев (это не сумма и не их арифметическое выражение),
- recommended — булево значение: true, если компания способна исполнить заказ по условиям закупки, false — если исполнение невозможно или малореалистично (рентабельность не учитывается),
- summary — краткий общий вывод (1–3 предложения на русском).
- essence — описание сути закупки (1–3 предложения о том, что и для чего закупает заказчик).

Важно:
- Если предмет закупки не относится к профилю компании (другие товары/услуги), считай закупку непрофильной: явно укажи это в комментариях, ставь recommended = false и decisionScore в нижнем диапазоне (например, 0.0–0.2).
- Отвечай строго одним JSON-объектом в формате UTF-8 без каких-либо пояснений, текста до или после JSON.
- Не добавляй комментарии, не используй поля, которых нет в схеме.
- Все комментарии должны быть на русском языке.";

    private const string PromptSuffix = @"Пожалуйста, проанализируй эту закупку для указанной компании и выдай результат строго в следующей JSON-структуре:

{
  ""scores"": {
    ""profitability"": {
      ""score"": number,
      ""shortComment"": ""string"",
      ""detailedComment"": ""string (Markdown)""
    },
    ""attractiveness"": {
      ""score"": number,
      ""shortComment"": ""string"",
      ""detailedComment"": ""string (Markdown)""
    },
    ""risk"": {
      ""score"": number,
      ""shortComment"": ""string"",
      ""detailedComment"": ""string (Markdown)""
    }
  },
  ""decisionScore"": number,
  ""recommended"": boolean,
  ""essence"": ""string"",
  ""summary"": ""string""
}

Где:
- все числа в диапазоне от 0.0 до 1.0,
- decisionScore — независимый интегральный балл, не являющийся суммой или усреднением других показателей,
- recommended отражает лишь способность компании исполнить заказ (не учитывай рентабельность),
- essence — короткое описание сути закупки (что закупается и для чего),
- все комментарии — на русском языке,
- detailedComment — полноценный Markdown-текст с переносами строк.";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true
    };

    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly AttachmentDownloadService _attachmentDownloadService;
    private readonly AttachmentContentExtractor _attachmentContentExtractor;
    private readonly AttachmentMarkdownService _attachmentMarkdownService;
    private readonly OpenAiOptions _options;
    private readonly EventBusOptions _eventBusOptions;
    private readonly IEventBusPublisher _eventBusPublisher;
    private readonly ILogger<NoticeAnalysisService> _logger;

    public NoticeAnalysisService(
        HttpClient httpClient,
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        AttachmentDownloadService attachmentDownloadService,
        AttachmentContentExtractor attachmentContentExtractor,
        AttachmentMarkdownService attachmentMarkdownService,
        IOptions<OpenAiOptions> options,
        IOptions<EventBusOptions> eventBusOptions,
        IEventBusPublisher eventBusPublisher,
        ILogger<NoticeAnalysisService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _attachmentDownloadService = attachmentDownloadService ?? throw new ArgumentNullException(nameof(attachmentDownloadService));
        _attachmentContentExtractor = attachmentContentExtractor ?? throw new ArgumentNullException(nameof(attachmentContentExtractor));
        _attachmentMarkdownService = attachmentMarkdownService ?? throw new ArgumentNullException(nameof(attachmentMarkdownService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _eventBusOptions = eventBusOptions?.Value ?? throw new ArgumentNullException(nameof(eventBusOptions));
        _eventBusPublisher = eventBusPublisher ?? throw new ArgumentNullException(nameof(eventBusPublisher));
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

        var requestQueueName = _eventBusOptions.ResolveNoticeAnalysisRequestQueueName();
        if (!_eventBusOptions.Enabled || string.IsNullOrWhiteSpace(requestQueueName))
        {
            throw new NoticeAnalysisException(
                "Очередь для анализа не настроена. Обратитесь к администратору системы.",
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
            var queueMessage = new NoticeAnalysisQueueMessage
            {
                AnalysisId = analysis.Id,
                NoticeId = noticeId,
                UserId = userId,
                CreatedAt = now,
                Force = force
            };

            await _eventBusPublisher.PublishNoticeAnalysisAsync(queueMessage, cancellationToken);

            return ToResponse(analysis);
        }
        catch (NoticeAnalysisException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue analysis for notice {NoticeId}", noticeId);

            analysis.Status = NoticeAnalysisStatus.Failed;
            analysis.Error = ex.Message;
            analysis.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            throw new NoticeAnalysisException(
                "Не удалось поставить задачу на анализ. Попробуйте повторить позже.",
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
                null,
                null,
                null,
                null);
        }

        return ToResponse(analysis);
    }

    public async Task ProcessQueueMessageAsync(NoticeAnalysisQueueMessage message, CancellationToken cancellationToken)
    {
        if (message.AnalysisId == Guid.Empty || message.NoticeId == Guid.Empty || string.IsNullOrWhiteSpace(message.UserId))
        {
            _logger.LogWarning("Получено сообщение анализа с неполными данными: {@Message}", message);
            return;
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var analysis = await context.NoticeAnalyses
            .FirstOrDefaultAsync(a => a.Id == message.AnalysisId, cancellationToken);

        if (analysis is null)
        {
            _logger.LogWarning("Анализ {AnalysisId} для закупки {NoticeId} не найден в базе", message.AnalysisId, message.NoticeId);
            return;
        }

        try
        {
            var user = await context.Users
                .Include(u => u.Regions)
                .FirstOrDefaultAsync(u => u.Id == analysis.UserId, cancellationToken)
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
                .FirstOrDefaultAsync(n => n.Id == analysis.NoticeId, cancellationToken)
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

            var regions = user.Regions
                .Select(r => UserCompanyService.ResolveRegionName(r.Region))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Where(r => r.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var prompt = BuildPrompt(
                notice,
                user.CompanyInfo!,
                regions,
                attachments,
                activeVersion.ProcedureWindow,
                out var attachmentContents);

            var answer = await RequestAnalysisAsync(prompt, attachmentContents, cancellationToken);
            var structuredResult = DeserializeTenderAnalysisResult(answer);
            var serializedResult = JsonSerializer.Serialize(structuredResult, SerializerOptions);

            analysis.Status = NoticeAnalysisStatus.Completed;
            analysis.Result = serializedResult;
            analysis.DecisionScore = structuredResult.DecisionScore;
            analysis.Recommended = structuredResult.Recommended;
            analysis.CompletedAt = DateTime.UtcNow;
            analysis.UpdatedAt = analysis.CompletedAt.Value;
            analysis.Error = null;

            await context.SaveChangesAsync(cancellationToken);
            await PublishAnalysisResultAsync(analysis, cancellationToken);
        }
        catch (NoticeAnalysisException ex)
        {
            await MarkAnalysisFailedAsync(context, analysis, ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process analysis {AnalysisId} from queue", message.AnalysisId);
            await MarkAnalysisFailedAsync(context, analysis, ex.Message, cancellationToken);
        }
    }

    public async Task<int> ResetStuckAnalysesAsync(CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-5);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stuckAnalyses = await context.NoticeAnalyses
            .Where(a => a.Status == NoticeAnalysisStatus.InProgress && a.UpdatedAt < threshold)
            .ToListAsync(cancellationToken);

        if (stuckAnalyses.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var analysis in stuckAnalyses)
        {
            analysis.Status = NoticeAnalysisStatus.Failed;
            analysis.Error = "Задача анализа сброшена: очередь пуста или обработчик недоступен.";
            analysis.UpdatedAt = now;
            analysis.CompletedAt = null;
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var analysis in stuckAnalyses)
        {
            try
            {
                await PublishAnalysisResultAsync(analysis, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось отправить результат сброса для анализа {AnalysisId}", analysis.Id);
            }
        }

        return stuckAnalyses.Count;
    }

    private async Task PublishAnalysisResultAsync(NoticeAnalysis analysis, CancellationToken cancellationToken)
    {
        var message = new NoticeAnalysisResultMessage
        {
            AnalysisId = analysis.Id,
            NoticeId = analysis.NoticeId,
            UserId = analysis.UserId,
            Status = analysis.Status,
            HasResult = analysis.Status == NoticeAnalysisStatus.Completed && !string.IsNullOrWhiteSpace(analysis.Result),
            Error = analysis.Error,
            UpdatedAt = analysis.UpdatedAt
        };

        await _eventBusPublisher.PublishNoticeAnalysisResultAsync(message, cancellationToken);
    }

    private async Task MarkAnalysisFailedAsync(
        NoticeDbContext context,
        NoticeAnalysis analysis,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        analysis.Status = NoticeAnalysisStatus.Failed;
        analysis.Error = errorMessage;
        analysis.UpdatedAt = DateTime.UtcNow;
        analysis.CompletedAt = null;

        await context.SaveChangesAsync(cancellationToken);
        await PublishAnalysisResultAsync(analysis, cancellationToken);
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
        IReadOnlyCollection<string> attachmentContents,
        CancellationToken cancellationToken)
    {
        var instructions = StructuredResponseInstructions;

        var content = new List<object>
        {
            new
            {
                type = "input_text",
                text = prompt
            }
        };

        foreach (var attachmentContent in attachmentContents)
        {
            content.Add(new
            {
                type = "input_text",
                text = attachmentContent
            });
        }

        var requestBody = new
        {
            model = _options.Model,
            instructions,
            input = new[]
            {
                new
                {
                    role = "user",
                    content
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

    private TenderAnalysisResult DeserializeTenderAnalysisResult(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new NoticeAnalysisException(
                "Сервис анализа вернул пустой ответ.",
                false);
        }

        if (TryDeserializeTenderAnalysisResult(answer, out var parsed))
        {
            return parsed;
        }

        throw new NoticeAnalysisException(
            "Сервис анализа вернул ответ в неверном формате.",
            false);
    }

    internal static TenderAnalysisResult? TryParseTenderAnalysisResult(string? json)
    {
        return TryDeserializeTenderAnalysisResult(json, out var parsed)
            ? parsed
            : null;
    }

    private static bool TryDeserializeTenderAnalysisResult(
        string? answer,
        out TenderAnalysisResult? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        if (TryDeserialize(answer, out result))
        {
            return true;
        }

        var startIndex = answer.IndexOf('{');
        var endIndex = answer.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            var json = answer.Substring(startIndex, endIndex - startIndex + 1);
            if (TryDeserialize(json, out result))
            {
                return true;
            }
        }

        return false;

        static bool TryDeserialize(string json, out TenderAnalysisResult? parsed)
        {
            try
            {
                parsed = JsonSerializer.Deserialize<TenderAnalysisResult>(json, SerializerOptions);
                if (parsed is null)
                {
                    return false;
                }

                NormalizeTenderAnalysisResult(parsed);
                return true;
            }
            catch (JsonException)
            {
                parsed = null;
                return false;
            }
        }
    }

    private static void NormalizeTenderAnalysisResult(TenderAnalysisResult result)
    {
        result.Scores ??= new TenderScores();
        result.Scores.Profitability ??= new ScoreSection();
        result.Scores.Attractiveness ??= new ScoreSection();
        result.Scores.Risk ??= new ScoreSection();
    }

    private string BuildPrompt(
        Notice notice,
        string companyInfo,
        IReadOnlyCollection<string> regions,
        IReadOnlyCollection<NoticeAttachment> attachments,
        ProcedureWindow? procedureWindow,
        out List<string> attachmentContents)
    {
        var builder = new StringBuilder();
        attachmentContents = BuildAttachmentContents(attachments);

        builder.AppendLine("ПРОФИЛЬ КОМПАНИИ:");
        builder.AppendLine(companyInfo.Trim());
        builder.AppendLine();

        builder.AppendLine("ЦЕЛЕВЫЕ РЕГИОНЫ КОМПАНИИ:");
        builder.AppendLine(regions.Count > 0 ? string.Join(", ", regions) : "не указаны");
        builder.AppendLine();

        builder.AppendLine("ОПИСАНИЕ ЗАКУПКИ:");
        builder.AppendLine($"Номер закупки: {notice.PurchaseNumber}");
        builder.AppendLine($"Предмет закупки: {notice.PurchaseObjectInfo ?? "не указан"}");

        var noticeRegion = UserCompanyService.ResolveRegionName(notice.Region) ??
             "не указан";
        builder.AppendLine($"Регион: {noticeRegion}");
        builder.AppendLine($"Площадка: {notice.EtpName ?? "не указана"}");

        if (notice.PublishDate is not null)
        {
            builder.AppendLine($"Дата публикации: {notice.PublishDate:yyyy-MM-dd HH:mm}");
        }

        if (notice.MaxPrice is not null)
        {
            var priceString = notice.MaxPrice.Value.ToString("N2", CultureInfo.InvariantCulture);
            builder.AppendLine($"Начальная цена: {priceString}".Trim());
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
        builder.AppendLine("ТРЕБОВАНИЯ К УЧАСТНИКАМ:");
        builder.AppendLine(BuildRequirementsSection(notice));

        builder.AppendLine();
        builder.AppendLine("ДОКУМЕНТЫ ЗАКУПКИ:");
        builder.AppendLine("Перечислены файлы из карточки закупки (тип, описание, имя, дата, размер). Полное содержимое приложено отдельными входами ниже.");

        var index = 0;
        foreach (var attachment in OrderAttachmentsForPrompt(attachments))
        {
            index++;
            builder.AppendLine($"- #{index}: {FormatAttachmentSummary(attachment)}");
        }

        builder.AppendLine();
        builder.AppendLine(PromptSuffix);

        return builder.ToString();
    }

    private List<string> BuildAttachmentContents(IReadOnlyCollection<NoticeAttachment> attachments)
    {
        var result = new List<string>();
        var totalCharacters = 0;
        var index = 0;
        var limitNoticeAdded = false;

        foreach (var attachment in OrderAttachmentsForPrompt(attachments))
        {
            if (string.IsNullOrWhiteSpace(attachment.MarkdownContent))
            {
                continue;
            }

            index++;

            var sanitized = attachment.MarkdownContent.Replace("\r", string.Empty);
            var maxLength = Math.Min(MaxAttachmentCharacters, MaxTotalAttachmentCharacters - totalCharacters);

            if (maxLength <= 0)
            {
                result.Add("[Дальнейшее содержимое опущено из-за ограничения размера.]");
                limitNoticeAdded = true;
                break;
            }

            var excerpt = sanitized.Length > maxLength
                ? sanitized[..maxLength] + " …"
                : sanitized;

            result.Add($"### Вложение {index}: {attachment.FileName ?? "без имени"}\n{excerpt.Trim()}");

            totalCharacters += Math.Min(sanitized.Length, maxLength);

            if (totalCharacters >= MaxTotalAttachmentCharacters)
            {
                result.Add("[Остальные вложения не включены, чтобы сохранить размер запроса.]");
                limitNoticeAdded = true;
                break;
            }
        }

        if (result.Count == 0 && !limitNoticeAdded)
        {
            result.Add("[Содержимое вложений не подготовлено.]");
        }

        return result;
    }

    private string BuildRequirementsSection(Notice notice)
    {
        if (string.IsNullOrWhiteSpace(notice.RawJson))
        {
            return "Нет данных о требованиях к участникам.";
        }

        try
        {
            var notification = JsonSerializer.Deserialize<EpNotificationEf2020>(notice.RawJson, SerializerOptions);
            var requirements = notification?.NotificationInfo?.RequirementsInfo?.Items;

            if (requirements is null || requirements.Count == 0)
            {
                return "Требования к участникам в карточке не указаны.";
            }

            var builder = new StringBuilder();
            var index = 0;

            foreach (var requirement in requirements)
            {
                index++;

                var titleParts = new List<string>();

                if (!string.IsNullOrWhiteSpace(requirement.PreferenseRequirementInfo?.ShortName))
                {
                    titleParts.Add(requirement.PreferenseRequirementInfo.ShortName!);
                }

                if (!string.IsNullOrWhiteSpace(requirement.PreferenseRequirementInfo?.Name))
                {
                    titleParts.Add(requirement.PreferenseRequirementInfo.Name!);
                }

                var header = titleParts.Count > 0
                    ? string.Join(" — ", titleParts)
                    : $"Требование {index}";

                builder.AppendLine($"- {header}");

                if (requirement.ReqValue is not null)
                {
                    builder.AppendLine($"  Значение/размер: {requirement.ReqValue.Value}");
                }

                if (!string.IsNullOrWhiteSpace(requirement.Content))
                {
                    builder.AppendLine($"  Описание: {requirement.Content.Trim()}");
                }

                if (requirement.AddRequirements?.Items is { Count: > 0 })
                {
                    foreach (var addRequirement in requirement.AddRequirements.Items)
                    {
                        var addTitleParts = new List<string>();

                        if (!string.IsNullOrWhiteSpace(addRequirement.ShortName))
                        {
                            addTitleParts.Add(addRequirement.ShortName!);
                        }

                        if (!string.IsNullOrWhiteSpace(addRequirement.Name))
                        {
                            addTitleParts.Add(addRequirement.Name!);
                        }

                        var addHeader = addTitleParts.Count > 0
                            ? string.Join(" — ", addTitleParts)
                            : "Дополнительное требование";

                        if (!string.IsNullOrWhiteSpace(addRequirement.Content))
                        {
                            builder.AppendLine($"  {addHeader}: {addRequirement.Content.Trim()}");
                        }
                        else
                        {
                            builder.AppendLine($"  {addHeader}");
                        }
                    }
                }
            }

            return builder.ToString().Trim();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse requirements for notice {NoticeId}", notice.Id);
            return "Не удалось разобрать требования к участникам из карточки.";
        }
    }

    private static string FormatAttachmentSummary(NoticeAttachment attachment)
    {
        var parts = new List<string>();

        var type = FirstNonEmpty(
            attachment.DocumentKindName,
            attachment.DocumentKindCode,
            "не указан");
        parts.Add($"Тип: {type}");

        if (!string.IsNullOrWhiteSpace(attachment.Description))
        {
            parts.Add($"Описание: {attachment.Description.Trim()}");
        }

        parts.Add($"Файл: {attachment.FileName ?? "не указан"}");

        if (attachment.DocumentDate is not null)
        {
            parts.Add($"Дата: {attachment.DocumentDate:yyyy-MM-dd}");
        }

        parts.Add($"Размер: {FormatFileSize(attachment.FileSize)}");

        return string.Join("; ", parts);
    }

    private static string FormatFileSize(long? size)
    {
        if (size is null)
        {
            return "неизвестен";
        }

        var value = (double)size.Value;
        var units = new[] { "Б", "КБ", "МБ", "ГБ" };
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
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

    private static NoticeAnalysisResponse ToResponse(
        NoticeAnalysis analysis,
        string? prompt = null,
        TenderAnalysisResult? structuredResult = null)
    {
        var hasAnswer = analysis.Status == NoticeAnalysisStatus.Completed &&
                        !string.IsNullOrWhiteSpace(analysis.Result);

        structuredResult ??= TryParseTenderAnalysisResult(analysis.Result);
        var decisionScore = analysis.DecisionScore ?? structuredResult?.DecisionScore;
        var recommended = analysis.Recommended ?? structuredResult?.Recommended;

        return new NoticeAnalysisResponse(
            analysis.NoticeId,
            analysis.Status,
            hasAnswer,
            analysis.Result,
            analysis.Error,
            analysis.UpdatedAt,
            analysis.CompletedAt,
            prompt,
            structuredResult,
            decisionScore,
            recommended);
    }
}
