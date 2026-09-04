using Dpm.Marketplace.Application.Administration;
using Dpm.Marketplace.Application.Contracts;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Marketplace.Api;

public sealed record SuspendStoreRequest(string Reason);

[ApiController]
[Route("api/v1/admin/stores")]
[Authorize(Policy = AuthorizationPolicies.StaffOnly)]
public sealed class AdminStoresController(ISender sender) : ControllerBase
{
    [HttpPost("{id:guid}/kyc/approve")]
    [ProducesResponseType(typeof(SellerProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileDto>> ApproveKyc(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ApproveKycCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/kyc/reject")]
    [ProducesResponseType(typeof(SellerProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileDto>> RejectKyc(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RejectKycCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/bank/verify")]
    [ProducesResponseType(typeof(SellerProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileDto>> VerifyBank(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new VerifyBankCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StoreDto>> Suspend(Guid id, SuspendStoreRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new SuspendStoreCommand(id, request.Reason), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/reinstate")]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StoreDto>> Reinstate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ReinstateStoreCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
