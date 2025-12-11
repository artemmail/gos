using System;
using System.Collections.Generic;

namespace Zakupki.Fetcher.Data.Entities;

public class Company
{
    public Guid Id { get; set; }

    public string Inn { get; set; } = null!;

    public string? Name { get; set; }

    public string? Address { get; set; }

    public byte? Region { get; set; }

    public ICollection<Notice> Notices { get; set; } = new List<Notice>();
}
