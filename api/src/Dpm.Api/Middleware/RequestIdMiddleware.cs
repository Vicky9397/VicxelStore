namespace Dpm.Api.Middleware;

/// <summary>Echoes a request correlation id on every response (spec 06 section 6.1).</summary>
public sealed class RequestIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming))
        {
            context.TraceIdentifier = incoming.ToString();
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
