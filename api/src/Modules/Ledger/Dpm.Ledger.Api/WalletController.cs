using Dpm.Ledger.Application.Contracts;
using Dpm.Ledger.Application.Queries;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Ledger.Api;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class WalletController(ISender sender) : ControllerBase
{
    /// <summary>Pending and available earnings, derived from the ledger on every read.</summary>
    [HttpGet("wallet")]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WalletDto>> GetWallet(
        [FromQuery] string currency = "INR",
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetMyWalletQuery(currency), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpGet("ledger")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerLineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerLineDto>>> GetStatement(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetMyStatementQuery(limit), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}

[ApiController]
[Route("api/v1/admin/finance")]
[Authorize(Policy = AuthorizationPolicies.StaffOnly)]
public sealed class FinanceController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Reports any transaction whose debits and credits disagree. Zero
    /// discrepancies is a release gate, not a target (spec 09C).
    /// </summary>
    [HttpGet("reconciliation")]
    [ProducesResponseType(typeof(ReconciliationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconciliationDto>> Reconciliation(CancellationToken ct)
    {
        var result = await sender.Send(new RunReconciliationQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
