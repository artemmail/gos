using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Services;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/user-company")]
[Authorize]
public sealed class UserCompanyController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserCompanyService _userCompanyService;

    public UserCompanyController(UserManager<ApplicationUser> userManager, UserCompanyService userCompanyService)
    {
        _userManager = userManager;
        _userCompanyService = userCompanyService;
    }

    [HttpGet]
    public async Task<ActionResult<UserCompanyProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await _userCompanyService.GetProfileAsync(userId, cancellationToken);
        return Ok(new UserCompanyProfileResponse
        {
            CompanyInfo = profile.CompanyInfo,
            Regions = new(profile.Regions),
            AvailableRegions = new(_userCompanyService.GetAvailableRegions())
        });
    }

    [HttpPut]
    public async Task<ActionResult<UserCompanyProfileResponse>> UpdateProfile(
        [FromBody] UpdateUserCompanyProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await _userCompanyService.UpdateProfileAsync(
            userId,
            request.CompanyInfo,
            request.Regions,
            cancellationToken);

        return Ok(new UserCompanyProfileResponse
        {
            CompanyInfo = profile.CompanyInfo,
            Regions = new(profile.Regions),
            AvailableRegions = new(_userCompanyService.GetAvailableRegions())
        });
    }
}
