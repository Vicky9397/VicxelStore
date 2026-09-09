using Dpm.Payments.Application.Webhooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Payments.Api;

/// <summary>
/// Provider callbacks. This is the only path that can capture a payment, and it
/// trusts nothing that fails signature verification.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Returns 200 for anything that verified, whether or not it changed state,
    /// because a provider retries on a non-2xx and the inbox already guarantees
    /// exactly-once application (spec 06 section 6.5). A failed signature gets
    /// 401 and no detail, so the endpoint cannot be used as a signing oracle.
    /// </summary>
    [HttpPost("{provider}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Receive(string provider, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        var headers = Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = await sender.Send(new HandleWebhookCommand(provider, rawBody, headers), ct);
        return result.IsSuccess ? Ok() : Unauthorized();
    }
}
