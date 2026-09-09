using Dpm.Downloads.Application;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dpm.Downloads.Api;

[ApiController]
[Route("api/v1/licenses")]
[Authorize(Policy = AuthorizationPolicies.EmailVerified)]
[EnableRateLimiting("download")]
public sealed class DownloadsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Issues a signed, short-lived URL for a file the caller has a license for.
    /// The refusal codes are the ones the buyer's client acts on: 403 when there
    /// is no entitlement, 429 when the quota is spent, 409 while the scan is
    /// still running and 410 once a file has been quarantined (spec 02 DL-02).
    /// </summary>
    [HttpGet("{licenseId:guid}/download")]
    [ProducesResponseType(typeof(DownloadUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<DownloadUrlDto>> Issue(
        Guid licenseId,
        [FromQuery] Guid fileId,
        CancellationToken ct)
    {
        var result = await sender.Send(new IssueDownloadUrlQuery(licenseId, fileId), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
