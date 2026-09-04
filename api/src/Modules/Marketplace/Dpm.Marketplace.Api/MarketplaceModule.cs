using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Application.CreateStore;
using Dpm.Marketplace.Contracts;
using Dpm.Marketplace.Infrastructure;
using Dpm.Marketplace.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Marketplace.Api;

public static class MarketplaceModule
{
    public static IServiceCollection AddMarketplaceModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MarketplaceDbContext>(builder =>
            builder.UseSqlServer(configuration.GetConnectionString("Database")));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CreateStoreCommand).Assembly);
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(CreateStoreCommand).Assembly);

        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IMarketplaceUnitOfWork, MarketplaceUnitOfWork>();
        services.AddScoped<ICurrentSeller, CurrentSeller>();
        services.AddScoped<IStoreDirectory, StoreDirectory>();

        return services;
    }
}
