using System;

namespace Zakupki.Fetcher.Data.Entities;

public class PostalIndex
{
    public int Id { get; set; }

    public int Index { get; set; }

    public string? OPSName { get; set; }

    public string? OPSType { get; set; }

    public int? OPSSubm { get; set; }

    public string? Region { get; set; }

    public byte? RegionId { get; set; }

    public string? Autonom { get; set; }

    public string? Area { get; set; }

    public string? City { get; set; }

    public string? City1 { get; set; }

    public DateTime? ActDate { get; set; }

    public string? IndexOld { get; set; }
}
