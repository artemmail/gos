namespace FabrikantGrabber.Models;

public sealed class SearchDownloadResult
{
    public string JsonPath { get; set; } = string.Empty;
    public FabrikantSearchResult SearchResult { get; set; } = new();
}
