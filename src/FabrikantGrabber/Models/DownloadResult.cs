using System.Collections.Generic;

namespace FabrikantGrabber.Models;

public sealed class DownloadResult
{
    public string JsonPath { get; init; } = default!;
    public string DocumentsFolder { get; init; } = default!;
    public List<string> DownloadedFiles { get; init; } = new();
}
