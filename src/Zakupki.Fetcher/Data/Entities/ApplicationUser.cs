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

    public ICollection<ApplicationUserOkpd2Code> Okpd2Codes { get; set; } = new List<ApplicationUserOkpd2Code>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public ICollection<NoticeAnalysis> NoticeAnalyses { get; set; } = new List<NoticeAnalysis>();

    public ICollection<FavoriteNotice> FavoriteNotices { get; set; } = new List<FavoriteNotice>();

    public ICollection<UserQueryVector> QueryVectors { get; set; } = new List<UserQueryVector>();
}
