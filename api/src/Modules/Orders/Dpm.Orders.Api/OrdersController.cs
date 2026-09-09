using Dpm.Orders.Application.Contracts;
using Dpm.Orders.Application.Queries;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Orders.Api;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> ListMine(CancellationToken ct)
    {
        var result = await sender.Send(new ListMyOrdersQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpGet("orders/{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetOrderQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpGet("me/licenses")]
    [ProducesResponseType(typeof(IReadOnlyList<LicenseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LicenseDto>>> ListLicenses(CancellationToken ct)
    {
        var result = await sender.Send(new ListMyLicensesQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
