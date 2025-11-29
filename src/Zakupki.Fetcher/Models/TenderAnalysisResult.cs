namespace Zakupki.Fetcher.Models;

public class TenderAnalysisResult
{
    public TenderScores Scores { get; set; } = new();

    public double DecisionScore { get; set; }

    public bool Recommended { get; set; }

    /// <summary>
    /// Краткий общий вывод (1–3 предложения) для UI.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Краткое описание сути закупки (что и для чего закупается).
    /// </summary>
    public string Essence { get; set; } = string.Empty;
}

public class TenderScores
{
    public ScoreSection Profitability { get; set; } = new();

    public ScoreSection Attractiveness { get; set; } = new();

    public ScoreSection Risk { get; set; } = new();
}

public class ScoreSection
{
    /// <summary>
    /// Числовой балл, например от 0.0 до 1.0.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Краткий комментарий (одно-два предложения).
    /// </summary>
    public string ShortComment { get; set; } = string.Empty;

    /// <summary>
    /// Подробный комментарий в Markdown.
    /// </summary>
    public string DetailedComment { get; set; } = string.Empty;
}
