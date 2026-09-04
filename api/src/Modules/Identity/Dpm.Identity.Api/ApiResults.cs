using Dpm.BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Identity.Api;

/// <summary>
/// Translates Result failures into RFC 9457 problem+json responses using the
/// error model of spec 06 section 6.4.
/// </summary>
public static class ApiResults
{
    public static ActionResult Problem(Error error, HttpContext httpContext)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.Locked => StatusCodes.Status423Locked,
            ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = new ProblemDetails
        {
            Title = error.Message,
            Status = status,
            Type = $"https://vicxelstore.example/errors/{error.Code.ToLowerInvariant()}",
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
