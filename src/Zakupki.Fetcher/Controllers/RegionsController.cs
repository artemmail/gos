using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Services;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/regions")]
[AllowAnonymous]
public sealed class RegionsController : ControllerBase
{
    private readonly UserCompanyService _userCompanyService;

    public RegionsController(UserCompanyService userCompanyService)
    {
        _userCompanyService = userCompanyService;
    }

    [HttpGet]
    public ActionResult<List<RegionOptionResponse>> GetRegions()
    {
        var regions = _userCompanyService
            .GetAvailableRegions()
            .Select(region => new RegionOptionResponse
            {
                Code = region.Code,
                Name = region.Name
            })
            .ToList();

        return Ok(regions);
    }
}
