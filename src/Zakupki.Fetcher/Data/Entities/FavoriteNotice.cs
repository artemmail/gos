using System;

namespace Zakupki.Fetcher.Data.Entities;

public class FavoriteNotice
{
    public Guid Id { get; set; }

    public Guid NoticeId { get; set; }

    public Notice Notice { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
