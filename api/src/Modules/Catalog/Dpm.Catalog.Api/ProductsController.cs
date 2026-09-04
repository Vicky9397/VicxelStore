using Dpm.Catalog.Application.Contracts;
using Dpm.Catalog.Application.CreateProduct;
using Dpm.Catalog.Application.Lifecycle;
using Dpm.Catalog.Application.Queries;
using Dpm.Catalog.Application.UpdateProduct;
using Dpm.Catalog.Application.Variants;
using Dpm.Catalog.Application.Versions;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Catalog.Api;

public sealed record CreateProductRequest(
    string Title,
    string CategorySlug,
    string? Description,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<CreateVariantInput> Variants);

public sealed record UpdateProductRequest(
    string Title,
    string? Description,
    string CategorySlug,
    string? SeoTitle,
    string? SeoDescription);

public sealed record AddVariantRequest(
    string Name,
    decimal Price,
    string Currency,
    string LicenseType,
    int DownloadLimit);

public sealed record UpdateVariantRequest(
    string Name,
    decimal Price,
    string Currency,
    string LicenseType,
    int DownloadLimit,
    bool IsActive);

public sealed record ReleaseVersionRequest(string VersionNumber, string? Changelog);

public sealed record ScheduleRequest(DateTime PublishAtUtc);

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductPage), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductPage>> Search(
        [FromQuery(Name = "q")] string? query,
        [FromQuery(Name = "filter[category]")] string? category,
        [FromQuery(Name = "filter[price][gte]")] decimal? minPrice,
        [FromQuery(Name = "filter[price][lte]")] decimal? maxPrice,
        [FromQuery(Name = "filter[license]")] string? licenseType,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new SearchProductsQuery(query, category, minPrice, maxPrice, licenseType, sort, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> GetBySlug(string slug, CancellationToken ct)
    {
        var result = await sender.Send(new GetProductBySlugQuery(slug), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<SellerProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SellerProductDto>>> ListMine(CancellationToken ct)
    {
        var result = await sender.Send(new ListMyProductsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    /// <summary>The owner's view of their own product, available in any status.</summary>
    [HttpGet("{id:guid}/manage")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SellerProductDetailDto>> GetMine(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetMyProductQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EmailVerified)]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SellerProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateProductCommand(
                request.Title,
                request.CategorySlug,
                request.Description,
                request.Tags ?? [],
                request.Variants),
            ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetBySlug), new { slug = result.Value.Slug }, result.Value)
            : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPatch("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProductDto>> Update(
        Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateProductCommand(
                id, request.Title, request.Description, request.CategorySlug,
                request.SeoTitle, request.SeoDescription),
            ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/variants")]
    [Authorize]
    [ProducesResponseType(typeof(VariantDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<VariantDto>> AddVariant(
        Guid id, AddVariantRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new AddVariantCommand(
                id, request.Name, request.Price, request.Currency,
                request.LicenseType, request.DownloadLimit),
            ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPatch("{id:guid}/variants/{variantId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(VariantDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VariantDto>> UpdateVariant(
        Guid id, Guid variantId, UpdateVariantRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateVariantCommand(
                id, variantId, request.Name, request.Price, request.Currency,
                request.LicenseType, request.DownloadLimit, request.IsActive),
            ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/versions")]
    [Authorize]
    [ProducesResponseType(typeof(VersionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<VersionDto>> ReleaseVersion(
        Guid id, ReleaseVersionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new ReleaseVersionCommand(id, request.VersionNumber, request.Changelog), ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerProductDto>> Submit(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SubmitProductCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerProductDto>> Publish(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new PublishProductCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/unpublish")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProductDto>> Unpublish(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new UnpublishProductCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/schedule")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProductDto>> Schedule(
        Guid id, ScheduleRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ScheduleProductCommand(id, request.PublishAtUtc), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize]
    [ProducesResponseType(typeof(SellerProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProductDto>> Archive(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ArchiveProductCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}

[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken ct)
    {
        var result = await sender.Send(new ListCategoriesQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
