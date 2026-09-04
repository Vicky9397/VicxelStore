using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Application.Register;
using Dpm.Identity.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Identity.Api;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityInfrastructure(configuration);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly);
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(RegisterUserCommand).Assembly);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
