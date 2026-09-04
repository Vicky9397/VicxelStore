using Dpm.Marketplace.Application.Contracts;
using Dpm.Marketplace.Application.CreateStore;
using Dpm.Marketplace.Application.GetStore;
using Dpm.Marketplace.Application.Kyc;
using Dpm.Marketplace.Application.UpdateStore;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Marketplace.Api;

public sealed record CreateStoreRequest(string Slug, string Name, string? About);

public sealed record UpdateStoreRequest(string Name, string? About);

public sealed record BrandingRequest(string? LogoUrl, string? BannerUrl, string? ThemeJson);

public sealed record KycRequest(string LegalName);

public sealed record TaxInfoRequest(string TaxIdType, string TaxId);

public sealed record BankInfoRequest(string BankRef);

[ApiController]
[Route("api/v1/stores")]
public sealed class StoresController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StoreDto>> Create(CreateStoreRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateStoreCommand(request.Slug, request.Name, request.About), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetBySlug), new { slug = result.Value.Slug }, result.Value)
            : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoreDto>> GetBySlug(string slug, CancellationToken ct)
    {
        var result = await sender.Send(new GetStoreBySlugQuery(slug), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(MyStoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyStoreDto>> GetMine(CancellationToken ct)
    {
        var result = await sender.Send(new GetMyStoreQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPatch("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StoreDto>> Update(Guid id, UpdateStoreRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateStoreCommand(id, request.Name, request.About), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPut("{id:guid}/branding")]
    [Authorize]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StoreDto>> UpdateBranding(Guid id, BrandingRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateBrandingCommand(id, request.LogoUrl, request.BannerUrl, request.ThemeJson), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/kyc")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileDto>> SubmitKyc(Guid id, KycRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new SubmitKycCommand(id, request.LegalName), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPut("{id:guid}/tax-info")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileDto>> UpdateTaxInfo(Guid id, TaxInfoRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateTaxInfoCommand(id, request.TaxIdType, request.TaxId), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPut("{id:guid}/bank-info")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileDto>> UpdateBankInfo(Guid id, BankInfoRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateBankInfoCommand(id, request.BankRef), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
