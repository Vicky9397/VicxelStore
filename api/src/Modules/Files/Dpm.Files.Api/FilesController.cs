using System.Security.Cryptography;
using Dpm.Files.Application.Contracts;
using Dpm.Files.Application.Scan;
using Dpm.Files.Application.Upload;
using Dpm.SharedApi;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Files.Api;

public sealed record InitUploadRequest(Guid VariantId, string FileName, long SizeBytes, string Checksum);

[ApiController]
[Route("api/v1/files")]
[Authorize(Policy = AuthorizationPolicies.EmailVerified)]
public sealed class FilesController(ISender sender) : ControllerBase
{
    /// <summary>Maximum bytes accepted for a single part, matching the session's part size.</summary>
    private const int MaxPartBytes = 16 * 1024 * 1024;

    [HttpPost("init")]
    [ProducesResponseType(typeof(UploadSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UploadSessionDto>> Init(InitUploadRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new InitUploadCommand(request.VariantId, request.FileName, request.SizeBytes, request.Checksum), ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ApiResults.Problem(result.Error, HttpContext);
    }

    /// <summary>
    /// Accepts one part of a resumable upload. The body is the raw bytes; the
    /// part's SHA-256 is computed here rather than trusted from the client.
    /// </summary>
    [HttpPut("{uploadId:guid}/parts/{partNumber:int}")]
    [RequestSizeLimit(MaxPartBytes)]
    [ProducesResponseType(typeof(UploadSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UploadSessionDto>> UploadPart(
        Guid uploadId, int partNumber, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(buffer, ct)).ToLowerInvariant();
        buffer.Position = 0;

        var result = await sender.Send(
            new UploadPartCommand(uploadId, partNumber, buffer, (int)buffer.Length, checksum), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpPost("{uploadId:guid}/complete")]
    [ProducesResponseType(typeof(ProductFileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductFileDto>> Complete(Guid uploadId, CancellationToken ct)
    {
        var result = await sender.Send(new CompleteUploadCommand(uploadId), ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ApiResults.Problem(result.Error, HttpContext);
    }

    [HttpGet("{fileId:guid}/scan-status")]
    [ProducesResponseType(typeof(ProductFileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductFileDto>> ScanStatus(Guid fileId, CancellationToken ct)
    {
        var result = await sender.Send(new GetScanStatusQuery(fileId), ct);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.Problem(result.Error, HttpContext);
    }
}
