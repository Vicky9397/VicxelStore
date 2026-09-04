using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Api.Middleware;

/// <summary>
/// Maps unhandled exceptions to RFC 9457 problem+json (spec 06 section 6.4).
/// Validation failures become 422 VALIDATION_ERROR with field errors; everything
/// else becomes an opaque 500 INTERNAL (never leaks internals).
/// </summary>
public sealed partial class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException exception)
        {
            await WriteValidationProblem(context, exception);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception exception)
        {
            LogUnhandled(logger, exception);
            await WriteInternalProblem(context);
        }
    }

    private static async Task WriteValidationProblem(HttpContext context, ValidationException exception)
    {
        var problem = new ProblemDetails
        {
            Type = "https://vicxelstore.example/errors/validation",
            Title = "Validation failed",
            Status = StatusCodes.Status422UnprocessableEntity,
            Detail = "One or more fields are invalid.",
        };
        problem.Extensions["code"] = exception.Errors.Any(e => e.ErrorCode == "WEAK_PASSWORD")
            ? "WEAK_PASSWORD"
            : "VALIDATION_ERROR";
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["errors"] = exception.Errors
            .Select(e => new
            {
                field = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName),
                message = e.ErrorMessage,
            })
            .ToArray();

        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static async Task WriteInternalProblem(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Type = "https://vicxelstore.example/errors/internal",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
        };
        problem.Extensions["code"] = "INTERNAL";
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
