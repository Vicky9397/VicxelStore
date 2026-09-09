using Dpm.Ledger.Application.Abstractions;
using Dpm.Ledger.Application.Posting;
using Dpm.Ledger.Contracts;
using Dpm.Ledger.Infrastructure;
using Dpm.Ledger.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Ledger.Api;

public static class LedgerModule
{
    public static IServiceCollection AddLedgerModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<LedgerDbContext>(builder =>
            builder.UseSqlServer(configuration.GetConnectionString("Database")));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(PostSaleJournalHandler).Assembly);
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(PostSaleJournalHandler).Assembly);

        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<ILedgerUnitOfWork, LedgerUnitOfWork>();
        services.AddScoped<IBalanceQueries, BalanceQueries>();
        services.AddScoped<ILedgerDirectory, LedgerDirectory>();

        return services;
    }
}
