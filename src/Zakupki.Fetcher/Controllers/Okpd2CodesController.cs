using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Services;

namespace Zakupki.Fetcher.Controllers;

[ApiController]
[Route("api/okpd2-codes")]
[AllowAnonymous]
public sealed class Okpd2CodesController : ControllerBase
{
    private readonly Okpd2CodeService _okpd2CodeService;

    public Okpd2CodesController(Okpd2CodeService okpd2CodeService)
    {
        _okpd2CodeService = okpd2CodeService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Okpd2CodeResponse>>> GetCodes(CancellationToken cancellationToken)
    {
        var codes = await _okpd2CodeService.GetCodesAsync(cancellationToken);

        var response = codes
            .Select(code => new Okpd2CodeResponse
            {
                Code = code.Code,
                Name = code.Name
            })
            .ToList();

        return Ok(response);
    }
}
