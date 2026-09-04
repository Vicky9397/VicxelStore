using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Dpm.Api.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>
    /// Per-IP token buckets; auth endpoints are tighter than the global default
    /// (spec 08 section 8.9). Backed by Redis once the cache module lands; the
    /// in-memory limiter is correct only for a single instance.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type = "https://vicxelstore.example/errors/rate_limited",
                        title = "Too many requests",
                        status = StatusCodes.Status429TooManyRequests,
                        code = "RATE_LIMITED",
                        traceId = context.HttpContext.TraceIdentifier,
                    },
                    cancellationToken);
            };
        });

        return services;
    }
}
