using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Zakupki.Fetcher.Services;

/// <summary>Converts analysis Markdown into editable Word paragraphs and runs.</summary>
internal static class MarkdownDocxRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    public static IReadOnlyList<Paragraph> CreateParagraphs(string markdown)
    {
        var paragraphs = new List<Paragraph>();
        foreach (var block in Markdown.Parse(markdown, Pipeline))
        {
            AppendBlock(block, paragraphs, 0, null);
        }

        if (paragraphs.Count == 0)
        {
            paragraphs.Add(new Paragraph());
        }

        return paragraphs;
    }

    private static void AppendBlock(Block block, List<Paragraph> paragraphs, int depth, string? listMarker)
    {
        switch (block)
        {
            case ParagraphBlock paragraphBlock:
                paragraphs.Add(CreateParagraph(paragraphBlock.Inline, depth, listMarker));
                break;
            case HeadingBlock headingBlock:
                paragraphs.Add(CreateParagraph(headingBlock.Inline, depth, listMarker, bold: true,
                    fontSize: Math.Max(12, 18 - headingBlock.Level)));
                break;
            case ListBlock listBlock:
                var number = int.TryParse(listBlock.OrderedStart, out var start) ? start : 1;
                foreach (var item in listBlock.OfType<ListItemBlock>())
                {
                    var marker = listBlock.IsOrdered ? $"{number++}. " : "• ";
                    var first = true;
                    foreach (var child in item)
                    {
                        AppendBlock(child, paragraphs, depth + 1, first ? marker : null);
                        first = false;
                    }
                }
                break;
            case QuoteBlock quoteBlock:
                foreach (var child in quoteBlock)
                {
                    AppendBlock(child, paragraphs, depth + 1, listMarker);
                    listMarker = null;
                }
                break;
            case CodeBlock codeBlock:
                var code = new Paragraph(new ParagraphProperties(
                    new SpacingBetweenLines { After = "120" },
                    new Indentation { Left = "360" }));
                AppendRun(code, codeBlock.Lines.ToString(), new Format(Code: true));
                paragraphs.Add(code);
                break;
            case ThematicBreakBlock:
                paragraphs.Add(new Paragraph(new Run(new Text("────────"))));
                break;
            case HtmlBlock:
                // The UI does not render raw HTML from analysis content either.
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AppendBlock(child, paragraphs, depth, listMarker);
                    listMarker = null;
                }
                break;
        }
    }

    private static Paragraph CreateParagraph(ContainerInline? inline, int depth, string? marker,
        bool bold = false, int? fontSize = null)
    {
        var properties = new ParagraphProperties(
            new SpacingBetweenLines { After = "120" },
            new Justification { Val = JustificationValues.Left });
        if (depth > 0)
        {
            properties.Append(new Indentation
            {
                Left = (depth * 360).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Hanging = marker is null ? null : "240"
            });
        }

        var paragraph = new Paragraph(properties);
        if (marker is not null)
        {
            AppendRun(paragraph, marker, default);
        }

        AppendInlines(paragraph, inline, new Format(Bold: bold, FontSize: fontSize));
        return paragraph;
    }

    private static void AppendInlines(Paragraph paragraph, ContainerInline? container, Format format)
    {
        for (var inline = container?.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    AppendRun(paragraph, literal.Content.ToString(), format);
                    break;
                case EmphasisInline emphasis:
                    AppendInlines(paragraph, emphasis, format with
                    {
                        Bold = format.Bold || emphasis.DelimiterCount >= 2,
                        Italic = format.Italic || emphasis.DelimiterCount == 1
                    });
                    break;
                case CodeInline code:
                    AppendRun(paragraph, code.Content, format with { Code = true });
                    break;
                case LineBreakInline:
                    paragraph.Append(new Run(new Break()));
                    break;
                case LinkInline link when !link.IsImage:
                    AppendInlines(paragraph, link, format with { Link = true });
                    break;
                case HtmlInline:
                    // Match the UI's raw HTML handling.
                    break;
                case ContainerInline nested:
                    AppendInlines(paragraph, nested, format);
                    break;
                default:
                    AppendRun(paragraph, inline.ToString() ?? string.Empty, format);
                    break;
            }
        }
    }

    private static void AppendRun(Paragraph paragraph, string text, Format format)
    {
        if (text.Length == 0)
        {
            return;
        }

        var properties = new RunProperties();
        if (format.Bold) properties.Append(new Bold());
        if (format.Italic) properties.Append(new Italic());
        if (format.Code) properties.Append(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" });
        if (format.Link)
        {
            properties.Append(new Color { Val = "145DA0" });
            properties.Append(new Underline { Val = UnderlineValues.Single });
        }
        if (format.FontSize.HasValue)
        {
            properties.Append(new FontSize { Val = (format.FontSize.Value * 2).ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        var run = new Run();
        if (properties.HasChildren) run.RunProperties = properties;
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.Append(run);
    }

    private readonly record struct Format(bool Bold = false, bool Italic = false,
        bool Code = false, bool Link = false, int? FontSize = null);
}
