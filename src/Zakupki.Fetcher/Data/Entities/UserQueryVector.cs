using System;

namespace Zakupki.Fetcher.Data.Entities;

public class UserQueryVector
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!;

    public string Query { get; set; } = null!;

    public string? VectorJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
