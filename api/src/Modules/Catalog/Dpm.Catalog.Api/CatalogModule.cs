using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.CreateProduct;
using Dpm.Catalog.Application.Moderation;
using Dpm.Catalog.Contracts;
using Dpm.Catalog.Infrastructure;
using Dpm.Catalog.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Catalog.Api;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(builder =>
            builder.UseSqlServer(configuration.GetConnectionString("Database")));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CreateProductCommand).Assembly);
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(CreateProductCommand).Assembly);

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICatalogUnitOfWork, CatalogUnitOfWork>();
        services.AddScoped<IModerationLog, ModerationLog>();
        services.AddScoped<IProductQueries, ProductQueries>();
        services.AddScoped<ICurrentModerator, CurrentModerator>();
        services.AddScoped<ICatalogDirectory, CatalogDirectory>();

        return services;
    }
}
