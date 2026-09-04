using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Infrastructure.Email;
using Dpm.Identity.Infrastructure.Persistence;
using Dpm.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Identity.Infrastructure;

public static class IdentityInfrastructureModule
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(builder =>
            builder.UseSqlServer(configuration.GetConnectionString("Database")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<AppLinkOptions>(configuration.GetSection(AppLinkOptions.SectionName));

        services.AddSingleton<Dpm.BuildingBlocks.Domain.IClock, Dpm.BuildingBlocks.Infrastructure.SystemClock>();
        services.AddSingleton<IJwtSigningKeyProvider, RsaSigningKeyProvider>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        var smtpHost = configuration.GetSection(SmtpOptions.SectionName)["Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        services.AddScoped<IVerificationEmailComposer, VerificationEmailComposer>();
        services.AddScoped<Dpm.Identity.Contracts.IUserDirectory, UserDirectory>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserTokenRepository, UserTokenRepository>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();

        return services;
    }
}
