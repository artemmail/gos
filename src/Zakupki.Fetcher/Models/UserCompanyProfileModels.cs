using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Zakupki.Fetcher.Models;

public sealed class UserCompanyProfileResponse
{
    public string CompanyInfo { get; init; } = string.Empty;

    public List<string> Regions { get; init; } = new();

    public List<string> AvailableRegions { get; init; } = new();
}

public sealed class UpdateUserCompanyProfileRequest
{
    [StringLength(8000)]
    public string CompanyInfo { get; set; } = string.Empty;

    public List<string> Regions { get; set; } = new();
}
