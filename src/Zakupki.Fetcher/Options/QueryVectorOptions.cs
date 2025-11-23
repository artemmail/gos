namespace Zakupki.Fetcher.Options;

public sealed class QueryVectorOptions
{
    /// <summary>
    /// HTTP endpoint of the vectorization service that accepts POST batches
    /// with payloads [{ id, string }]. When specified, the dialog sends
    /// vectors directly without using RabbitMQ.
    /// </summary>
    public string? VectorizerUrl { get; set; }
}
