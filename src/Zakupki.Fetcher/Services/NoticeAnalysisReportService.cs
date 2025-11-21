using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeAnalysisReportService
{
    private const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly ILogger<NoticeAnalysisReportService> _logger;

    public NoticeAnalysisReportService(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        ILogger<NoticeAnalysisReportService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NoticeAnalysisReportFile> CreateAsync(
        Guid noticeId,
        string userId,
        CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var analysis = await context.NoticeAnalyses
            .AsNoTracking()
            .Include(a => a.Notice)
            .FirstOrDefaultAsync(
                a => a.NoticeId == noticeId && a.UserId == userId,
                cancellationToken);

        if (analysis is null)
        {
            throw new NoticeAnalysisException(
                "Анализ для выбранной закупки не найден.",
                true);
        }

        if (analysis.Status != NoticeAnalysisStatus.Completed || string.IsNullOrWhiteSpace(analysis.Result))
        {
            throw new NoticeAnalysisException(
                "Анализ ещё не завершён. Дождитесь результата перед выгрузкой отчёта.",
                true);
        }

        if (analysis.Notice is null)
        {
            _logger.LogWarning("Notice entity is missing for analysis {AnalysisId}", analysis.Id);
            throw new NoticeAnalysisException(
                "Не удалось загрузить данные закупки для отчёта.",
                false);
        }

        var structuredResult = NoticeAnalysisService.TryParseTenderAnalysisResult(analysis.Result);
        if (structuredResult is null)
        {
            throw new NoticeAnalysisException(
                "Результат анализа имеет устаревший формат и не может быть выгружен.",
                false);
        }

        var reportBytes = BuildWordDocument(analysis.Notice, analysis, structuredResult);
        var sanitizedBaseName = FileNameHelper.SanitizeFileName($"Анализ_{analysis.Notice.PurchaseNumber}");
        var fileName = $"{sanitizedBaseName}.docx";

        return new NoticeAnalysisReportFile(reportBytes, WordContentType, fileName);
    }

    private static byte[] BuildWordDocument(
        Notice notice,
        NoticeAnalysis analysis,
        TenderAnalysisResult result)
    {
        using var memoryStream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body ?? throw new InvalidOperationException("Document body is missing");

            AppendTitle(body, notice);
            AppendGeneralInfo(body, notice, analysis);
            AppendSummary(body, result);
            AppendDecision(body, result);

            var sections = BuildScoreSections(result);
            if (sections.Count > 0)
            {
                AppendSubHeading(body, "Оценка по критериям");
                body.AppendChild(BuildScoresTable(sections));
            }

            var detailedSections = sections
                .Where(section => !string.IsNullOrWhiteSpace(section.DetailedComment))
                .ToArray();

            if (detailedSections.Length > 0)
            {
                AppendSubHeading(body, "Подробные комментарии");
                foreach (var section in detailedSections)
                {
                    body.AppendChild(CreateParagraph(section.Title, bold: true));
                    body.AppendChild(CreateParagraph(section.DetailedComment!.Trim()));
                }
            }

            mainPart.Document.Save();
        }

        return memoryStream.ToArray();
    }

    private static void AppendTitle(Body body, Notice notice)
    {
        var purchaseNumber = notice.PurchaseNumber?.Trim();

        body.AppendChild(CreateParagraph(
            purchaseNumber is null
                ? "Анализ закупки"
                : $"Анализ закупки № {purchaseNumber}",
            bold: true,
            fontSize: 28));

        var purchaseObject = NormalizeText(notice.PurchaseObjectInfo);
        if (!string.IsNullOrWhiteSpace(purchaseObject))
        {
            body.AppendChild(CreateParagraph(purchaseObject!, italic: true));
        }
    }

    private static void AppendGeneralInfo(Body body, Notice notice, NoticeAnalysis analysis)
    {
        var entries = new List<(string Label, string? Value)>
        {
            ("Дата формирования", FormatDateTime(analysis.CompletedAt ?? DateTime.UtcNow)),
            ("Регион", NormalizeText(UserCompanyService.ResolveRegionName(notice.Region) ?? notice.Region)),
            ("Площадка", NormalizeText(notice.EtpName)),
            ("Окончание подачи заявок", FormatDateTime(notice.CollectingEnd)),
            ("НМЦК", FormatMaxPrice(notice))
        };

        foreach (var (label, value) in entries)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            body.AppendChild(CreateKeyValueParagraph(label, value!));
        }
    }

    private static void AppendSummary(Body body, TenderAnalysisResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Summary))
        {
            return;
        }

        AppendSubHeading(body, "Краткий вывод");
        body.AppendChild(CreateParagraph(result.Summary.Trim()));
    }

    private static void AppendDecision(Body body, TenderAnalysisResult result)
    {
        var decisionScore = FormatScore(result.DecisionScore);
        var recommendation = result.Recommended
            ? "Заказ подходит"
            : "Заказ не подходит";

        AppendSubHeading(body, "Итоговая оценка");
        body.AppendChild(CreateKeyValueParagraph("Итоговый балл", decisionScore));
        body.AppendChild(CreateKeyValueParagraph("Рекомендация", recommendation));
    }

    private static IReadOnlyList<ScoreSectionData> BuildScoreSections(TenderAnalysisResult result)
    {
        var sections = new List<ScoreSectionData>();

        if (result.Scores?.Profitability is not null)
        {
            sections.Add(CreateSection("Рентабельность", result.Scores.Profitability));
        }

        if (result.Scores?.Attractiveness is not null)
        {
            sections.Add(CreateSection("Привлекательность", result.Scores.Attractiveness));
        }

        if (result.Scores?.Risk is not null)
        {
            sections.Add(CreateSection("Риски", result.Scores.Risk));
        }

        return sections;
    }

    private static ScoreSectionData CreateSection(string title, ScoreSection section)
    {
        return new ScoreSectionData(
            title,
            FormatScore(section.Score),
            string.IsNullOrWhiteSpace(section.ShortComment) ? null : section.ShortComment.Trim(),
            string.IsNullOrWhiteSpace(section.DetailedComment) ? null : section.DetailedComment.Trim());
    }

    private static Table BuildScoresTable(IReadOnlyList<ScoreSectionData> sections)
    {
        var table = new Table();
        var borders = new TableBorders(
            new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },
            new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },
            new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },
            new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },
            new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 });

        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            borders));

        var headerRow = new TableRow();
        headerRow.Append(
            CreateHeaderCell("Критерий"),
            CreateHeaderCell("Балл"),
            CreateHeaderCell("Краткий комментарий"));
        table.AppendChild(headerRow);

        foreach (var section in sections)
        {
            var row = new TableRow();
            row.Append(
                CreateCell(section.Title),
                CreateCell(section.Score),
                CreateCell(section.ShortComment ?? "—"));
            table.AppendChild(row);
        }

        return table;
    }

    private static TableCell CreateHeaderCell(string text)
    {
        var run = new Run(new RunProperties(new Bold()), new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        var paragraph = new Paragraph(run);
        var cell = new TableCell(paragraph)
        {
            TableCellProperties = new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Auto },
                new Shading { Val = ShadingPatternValues.Clear, Fill = "DDDDDD" })
        };
        return cell;
    }

    private static TableCell CreateCell(string text)
    {
        var paragraph = new Paragraph();
        var run = new Run();
        AppendText(run, text);
        paragraph.Append(run);

        return new TableCell(paragraph)
        {
            TableCellProperties = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto })
        };
    }

    private static void AppendSubHeading(Body body, string text)
    {
        body.AppendChild(CreateParagraph(text, bold: true, fontSize: 18));
    }

    private static Paragraph CreateParagraph(string text, bool bold = false, int? fontSize = null, bool italic = false)
    {
        var paragraph = new Paragraph
        {
            ParagraphProperties = new ParagraphProperties
            {
                SpacingBetweenLines = new SpacingBetweenLines { After = "160" },
                Justification = new Justification { Val = JustificationValues.Left }
            }
        };

        var run = new Run();
        var properties = new RunProperties();

        if (bold)
        {
            properties.Append(new Bold());
        }

        if (italic)
        {
            properties.Append(new Italic());
        }

        if (fontSize.HasValue)
        {
            properties.Append(new FontSize { Val = (fontSize.Value * 2).ToString(CultureInfo.InvariantCulture) });
        }

        if (properties.HasChildren)
        {
            run.RunProperties = properties;
        }

        AppendText(run, text);
        paragraph.Append(run);
        return paragraph;
    }

    private static Paragraph CreateKeyValueParagraph(string label, string value)
    {
        var paragraph = new Paragraph
        {
            ParagraphProperties = new ParagraphProperties
            {
                SpacingBetweenLines = new SpacingBetweenLines { After = "120" },
                Justification = new Justification { Val = JustificationValues.Left }
            }
        };

        var labelRun = new Run
        {
            RunProperties = new RunProperties(new Bold())
        };
        AppendText(labelRun, $"{label}: ");

        var valueRun = new Run();
        AppendText(valueRun, value);

        paragraph.Append(labelRun, valueRun);
        return paragraph;
    }

    private static void AppendText(Run run, string text)
    {
        if (text is null)
        {
            return;
        }

        var lines = text
            .Replace('\r', '\n')
            .Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            run.Append(new Text(line ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
            if (i < lines.Length - 1)
            {
                run.Append(new Break());
            }
        }
    }

    private static string FormatDateTime(DateTime? dateTime)
    {
        if (dateTime is null)
        {
            return string.Empty;
        }

        var local = dateTime.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc).ToLocalTime()
            : dateTime.Value.ToLocalTime();

        return local.ToString("dd.MM.yyyy HH:mm", RussianCulture);
    }

    private static string FormatMaxPrice(Notice notice)
    {
        if (notice.MaxPrice is null)
        {
            return string.Empty;
        }

        return $"{notice.MaxPrice.Value.ToString("N2", RussianCulture)} руб.";
    }

    private static string FormatScore(double score)
    {
        var clamped = Math.Clamp(score, 0, 1);
        var scaled = Math.Round(clamped * 100) / 10;
        return scaled.ToString("0.0", RussianCulture);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ScoreSectionData(
        string Title,
        string Score,
        string? ShortComment,
        string? DetailedComment);
}
