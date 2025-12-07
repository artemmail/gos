using System.Collections.Generic;

namespace FabrikantGrabber.Models;

public sealed class FabrikantSearchResult
{
    public int TotalCount { get; set; }
    public List<FabrikantSearchItem> Procedures { get; set; } = new();
    public string RawHtml { get; set; } = string.Empty;
}
