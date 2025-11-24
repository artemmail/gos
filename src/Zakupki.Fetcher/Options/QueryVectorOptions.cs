namespace Zakupki.Fetcher.Options;

public sealed class QueryVectorOptions
{
    /// <summary>
    /// Service name used in outgoing vectorization requests.
    /// </summary>
    public string ServiceId { get; set; } = "AddUserSemanticReq";
}
