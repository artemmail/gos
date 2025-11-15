using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Zakupki.Fetcher.Data.Entities;

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool HasLifetimeAccess { get; set; }

    public string? CompanyInfo { get; set; }

    public ICollection<ApplicationUserRegion> Regions { get; set; } = new List<ApplicationUserRegion>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
