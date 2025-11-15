using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace Zakupki.Fetcher.Services;

public sealed class NoticeAnalysisService
{
    private const int MaxAttachmentCharacters = 4000;
    private const int MaxTotalAttachmentCharacters = 16000;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly OpenAiOptions _options;
    private readonly ILogger<NoticeAnalysisService> _logger;

    public NoticeAnalysisService(
        HttpClient httpClient,
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        IOptions<OpenAiOptions> options,
        ILogger<NoticeAnalysisService> logger)
    {
        _httpClient = httpClient;
        _dbContextFactory = dbContextFactory;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            if (!baseUri.AbsoluteUri.EndsWith('/'))
            {
                baseUri = new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);
            }

            _httpClient.BaseAddress = baseUri;
        }
    }

    public async Task<NoticeAnalysisResponse> AnalyzeAsync(Guid noticeId, string userId, bool force, CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new NoticeAnalysisException("API-ключ OpenAI не настроен. Обратитесь к администратору системы.", true);
        }

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
            throw new NoticeAnalysisException("Сначала скачайте вложения для закупки и конвертируйте их в Markdown.", true);
        }

        if (attachments.Any(a => string.IsNullOrWhiteSpace(a.MarkdownContent)))
        {
            throw new NoticeAnalysisException("Сконвертируйте все вложения в Markdown перед запуском анализа.", true);
        }

        var markdownAttachments = attachments;

        var existingAnalysis = await context.NoticeAnalyses
            .FirstOrDefaultAsync(a => a.NoticeId == noticeId && a.UserId == userId, cancellationToken);

        if (existingAnalysis is not null && !force)
        {
            if (existingAnalysis.Status == NoticeAnalysisStatus.InProgress)
            {
                return ToResponse(existingAnalysis);
            }

            if (existingAnalysis.Status == NoticeAnalysisStatus.Completed && !string.IsNullOrWhiteSpace(existingAnalysis.Result))
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
                .Select(r => r.Region)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Where(r => r.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var prompt = BuildPrompt(
                notice,
                user.CompanyInfo!,
                regions,
                markdownAttachments,
                activeVersion.ProcedureWindow);

            var answer = await RequestAnalysisAsync(prompt, cancellationToken);

            analysis.Status = NoticeAnalysisStatus.Completed;
            analysis.Result = answer;
            analysis.CompletedAt = DateTime.UtcNow;
            analysis.UpdatedAt = analysis.CompletedAt.Value;

            await context.SaveChangesAsync(cancellationToken);

            return ToResponse(analysis);
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

            throw new NoticeAnalysisException("Не удалось выполнить анализ закупки. Попробуйте повторить позже.", false, ex);
        }
    }

    public async Task<NoticeAnalysisResponse> GetStatusAsync(Guid noticeId, string userId, CancellationToken cancellationToken)
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
                null);
        }

        return ToResponse(analysis);
    }

    private async Task<string> RequestAnalysisAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _options.Model,
            input = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt }
                    }
                }
            },
            temperature = 0.2,
            max_output_tokens = 800,
            response_format = new { type = "text" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, SerializerOptions), Encoding.UTF8, "application/json")
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
            throw new Exception("Сервис анализа не вернул текстовый ответ.");
        }

        return answer.Trim();
    }

    private static string ExtractAnswer(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("output", out var outputElement))
        {
            var builder = new StringBuilder();
            foreach (var item in outputElement.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var contentArray))
                {
                    continue;
                }

                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textElement))
                    {
                        var text = textElement.GetString();
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

        if (document.RootElement.TryGetProperty("choices", out var choicesElement))
        {
            foreach (var choice in choicesElement.EnumerateArray())
            {
                if (choice.TryGetProperty("text", out var legacyTextElement))
                {
                    var text = legacyTextElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }

                if (choice.TryGetProperty("message", out var messageElement) &&
                    messageElement.TryGetProperty("content", out var contentElement))
                {
                    var text = contentElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
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
        builder.AppendLine("Ты — эксперт по государственным закупкам.");
        builder.AppendLine("Используя данные о закупке, профиле компании и вложениях, оцени, подходит ли закупка компании.");
        builder.AppendLine("Ответ должен быть на русском языке и учитывать соответствие предмета закупки, условий и регионов.");
        builder.AppendLine();

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
        builder.AppendLine($"Регион: {notice.Region ?? "не указан"}");
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
            builder.AppendLine($"Начальная цена: {notice.MaxPrice.Value.ToString("N2", CultureInfo.InvariantCulture)} {currency}".Trim());
        }

        if (!string.IsNullOrWhiteSpace(notice.Okpd2Code) || !string.IsNullOrWhiteSpace(notice.Okpd2Name))
        {
            builder.AppendLine($"ОКПД2: {notice.Okpd2Code ?? "-"} — {notice.Okpd2Name ?? "не указано"}");
        }

        if (!string.IsNullOrWhiteSpace(notice.KvrCode) || !string.IsNullOrWhiteSpace(notice.KvrName))
        {
            builder.AppendLine($"КВР: {notice.KvrCode ?? "-"} — {notice.KvrName ?? "не указано"}");
        }

        if (procedureWindow is not null)
        {
            if (procedureWindow.CollectingEnd is not null)
            {
                builder.AppendLine($"Окончание подачи заявок: {procedureWindow.CollectingEnd:yyyy-MM-dd HH:mm}");
            }

            if (!string.IsNullOrWhiteSpace(procedureWindow.SubmissionProcedureDateRaw))
            {
                builder.AppendLine($"Дата процедуры подачи: {procedureWindow.SubmissionProcedureDateRaw}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Фрагменты вложений (Markdown):");

        var totalCharacters = 0;
        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments.ElementAt(index);
            if (string.IsNullOrWhiteSpace(attachment.MarkdownContent))
            {
                continue;
            }

            builder.AppendLine($"### Вложение {index + 1}: {attachment.FileName}");

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

        builder.AppendLine();
        builder.AppendLine("Сформируй ответ в следующем виде:");
        builder.AppendLine("1. Итог: подходит ли закупка (подходит / не подходит / требуется уточнение).");
        builder.AppendLine("2. Обоснование: ключевые факторы соответствия или несоответствия.");
        builder.AppendLine("3. Рекомендации: что делать компании дальше.");
        builder.AppendLine("Ответ должен быть кратким, но информативным.");

        return builder.ToString();
    }

    private static NoticeAnalysisResponse ToResponse(NoticeAnalysis analysis)
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
            analysis.CompletedAt);
    }
}
