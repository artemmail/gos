namespace Zakupki.Fetcher.Options;

public sealed class NoticeEmbeddingOptions
{
    /// <summary>
    /// Enables background vectorization of notices via the shared vectorizer queue.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Service identifier passed to the vectorizer to distinguish notice updates from user queries.
    /// </summary>
    public string ServiceId { get; set; } = "NoticeEmbeddingUpdate";

    /// <summary>
    /// Source value stored in the NoticeEmbeddings table.
    /// </summary>
    public string Source { get; set; } = "notice_vectorizer";

    /// <summary>
    /// Maximum number of notices to send in a single batch.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Delay between polling cycles when there is no work to do, in seconds.
    /// </summary>
    public int IdleDelaySeconds { get; set; } = 30;
}
