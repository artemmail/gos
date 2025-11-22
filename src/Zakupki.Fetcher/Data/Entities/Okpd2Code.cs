using System;

namespace Zakupki.Fetcher.Data.Entities;

public class Okpd2Code
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }
}
