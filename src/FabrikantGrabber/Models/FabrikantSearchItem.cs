using System;

namespace FabrikantGrabber.Models;

public sealed class FabrikantSearchItem
{
    public string ProcedureId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Uri? Url { get; set; }
}
