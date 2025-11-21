using System;

namespace Zakupki.Fetcher.Models;

public sealed class VectorSearchMatch
{
    public Guid NoticeId { get; set; }

    public double Similarity { get; set; }
}
