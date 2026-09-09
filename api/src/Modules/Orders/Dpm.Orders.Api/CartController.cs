using Dpm.Orders.Application.CartCommands;
using Dpm.Orders.Application.Contracts;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Orders.Api;

public sealed record CartItemRequest(Guid VariantId);

public sealed record SaveForLaterRequest(bool SavedForLater);

[ApiController]
[Route("api/v1/cart")]
[Authorize]
public sealed class CartController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct)
    {
        var result = await sender.Send(new GetCartQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartDto>> AddItem(CartItemRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AddCartItemCommand(request.VariantId), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpDelete("items/{variantId:guid}")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> RemoveItem(Guid variantId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveCartItemCommand(variantId), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("items/{variantId:guid}/save-for-later")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> SaveForLater(
        Guid variantId, SaveForLaterRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveForLaterCommand(variantId, request.SavedForLater), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
