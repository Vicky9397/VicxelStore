using Dpm.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Dpm.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// RS256 bearer authentication validating against the module's signing key.
    /// Authorization is default-deny: the fallback policy requires an
    /// authenticated user unless an endpoint opts out with [AllowAnonymous]
    /// (spec 08 section 8.3).
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = "sub",
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwtSigningKeyProvider, IConfiguration>((options, keyProvider, configuration) =>
            {
                var jwtSection = configuration.GetSection(JwtOptions.SectionName);
                options.TokenValidationParameters.ValidIssuer = jwtSection["Issuer"] ?? "dpm-api";
                options.TokenValidationParameters.ValidAudience = jwtSection["Audience"] ?? "dpm-clients";
                options.TokenValidationParameters.IssuerSigningKey = keyProvider.SigningKey;
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
