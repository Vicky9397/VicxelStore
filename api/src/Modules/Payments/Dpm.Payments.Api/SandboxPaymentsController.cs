using Dpm.Payments.Application.Abstractions;
using Dpm.Payments.Application.Webhooks;
using Dpm.Payments.Infrastructure.Providers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Dpm.Payments.Api;

public sealed record SandboxConfirmRequest(string IntentId, decimal Amount, string Currency);

/// <summary>
/// Stands in for the buyer completing a charge at a hosted gateway. It builds a
/// correctly signed sandbox webhook and feeds it through the same verification
/// and inbox path a real provider's callback takes, so development exercises the
/// production flow rather than a shortcut around it.
///
/// Registered only outside production.
/// </summary>
[ApiController]
[Route("api/v1/dev/payments")]
[AllowAnonymous]
public sealed class SandboxPaymentsController(
    ISender sender,
    IPaymentProviderFactory providerFactory,
    IHostEnvironment environment)
    : ControllerBase
{
    [HttpPost("confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Confirm(SandboxConfirmRequest request, CancellationToken ct)
    {
        if (environment.IsProduction() || !providerFactory.IsSupported("sandbox"))
        {
            return NotFound();
        }

        var provider = (SandboxPaymentProvider)providerFactory.Resolve("sandbox");
        var payload = SandboxPaymentProvider.BuildSucceededPayload(
            request.IntentId, request.Amount, request.Currency);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Sandbox-Signature"] = provider.Sign(payload),
        };

        var result = await sender.Send(new HandleWebhookCommand("sandbox", payload, headers), ct);
        return result.IsSuccess ? Ok() : BadRequest();
    }
}
