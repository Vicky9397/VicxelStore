using Dpm.Catalog.Application.Contracts;
using Dpm.Catalog.Application.Moderation;
using Dpm.Catalog.Application.Queries;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Catalog.Api;

public sealed record RejectProductRequest(string Reason);

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = AuthorizationPolicies.ModeratorOnly)]
public sealed class AdminProductsController(ISender sender) : ControllerBase
{
    [HttpGet("moderation/queue")]
    [ProducesResponseType(typeof(IReadOnlyList<ModerationQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ModerationQueueItemDto>>> Queue(CancellationToken ct)
    {
        var result = await sender.Send(new ListModerationQueueQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("products/{id:guid}/review")]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProductDto>> BeginReview(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new BeginReviewCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("products/{id:guid}/approve")]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerProductDto>> Approve(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ApproveProductCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("products/{id:guid}/reject")]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SellerProductDto>> Reject(
        Guid id, RejectProductRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RejectProductCommand(id, request.Reason), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
