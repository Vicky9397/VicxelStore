namespace Dpm.Api.Middleware;

/// <summary>Baseline security headers (spec 08 section 8.10).</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await next(context);
    }
}
