using System;

namespace FabrikantGrabber.Models;

public sealed class DocumentationLink
{
    public Uri Url { get; set; } = default!;
    public string FileName { get; set; } = string.Empty;
}
