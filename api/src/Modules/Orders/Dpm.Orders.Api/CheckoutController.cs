using Dpm.Orders.Application.Checkout;
using Dpm.Orders.Application.Contracts;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dpm.Orders.Api;

public sealed record QuoteRequest(string BillingCountry);

public sealed record ConfirmRequest(
    string BillingCountry,
    string PaymentMethod,
    string? ReturnUrl,
    decimal? ExpectedGrandTotal);

[ApiController]
[Route("api/v1/checkout")]
[Authorize(Policy = AuthorizationPolicies.EmailVerified)]
[EnableRateLimiting("checkout")]
public sealed class CheckoutController(ISender sender, IdempotentRequests idempotency) : ControllerBase
{
    private const string ConfirmEndpoint = "POST /api/v1/checkout/confirm";

    [HttpPost("quote")]
    [ProducesResponseType(typeof(QuoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteDto>> Quote(QuoteRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CheckoutQuoteQuery(request.BillingCountry), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    /// <summary>
    /// Places the order and starts the payment. Requires an Idempotency-Key so a
    /// retry returns the original result instead of charging again
    /// (spec README A14).
    /// </summary>
    [HttpPost("confirm")]
    [ProducesResponseType(typeof(CheckoutResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status402PaymentRequired)]
    public async Task<ActionResult<CheckoutResultDto>> Confirm(ConfirmRequest request, CancellationToken ct)
    {
        var key = IdempotentRequests.ReadKey(Request);
        if (key is null)
        {
            return ApiResults.Problem(
                Dpm.BuildingBlocks.Application.Error.Validation(
                    "BAD_REQUEST",
                    $"The {IdempotentRequests.HeaderName} header is required on checkout."),
                HttpContext);
        }

        if (await idempotency.TryReplayAsync(key, ConfirmEndpoint, ct) is { } replayed)
        {
            return replayed;
        }

        var result = await sender.Send(
            new ConfirmCheckoutCommand(
                request.BillingCountry,
                request.PaymentMethod,
                request.ReturnUrl,
                request.ExpectedGrandTotal),
            ct);

        if (result.IsFailure)
        {
            return ApiResults.Problem(result.Error, HttpContext);
        }

        await idempotency.RememberAsync(key, ConfirmEndpoint, StatusCodes.Status200OK, result.Value, ct);
        return Ok(result.Value);
    }
}
