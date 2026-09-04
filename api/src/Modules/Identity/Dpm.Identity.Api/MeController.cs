using Dpm.Identity.Application.Contracts;
using Dpm.Identity.Application.Me;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Identity.Api;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class MeController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Get(CancellationToken ct)
    {
        var result = await sender.Send(new GetMeQuery(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.Problem(result.Error, HttpContext);
    }
}
