using Dpm.Orders.Application.Abstractions;
using Dpm.Orders.Application.CartCommands;
using Dpm.Orders.Application.Checkout;
using Dpm.Orders.Application.Queries;
using Dpm.Orders.Contracts;
using Dpm.Orders.Infrastructure;
using Dpm.Orders.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Orders.Api;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<OrdersDbContext>(builder =>
            builder.UseSqlServer(configuration.GetConnectionString("Database")));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ConfirmCheckoutCommand).Assembly);
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(ConfirmCheckoutCommand).Assembly);

        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrdersUnitOfWork, OrdersUnitOfWork>();
        services.AddScoped<IInvoiceNumbers, InvoiceNumbers>();
        services.AddScoped<ICurrentBuyer, CurrentBuyer>();
        services.AddScoped<IOrderDirectory, OrderDirectory>();
        services.AddScoped<CartService>();
        services.AddScoped<CheckoutPricing>();
        services.AddScoped<OrderProjector>();
        services.AddScoped<IdempotentRequests>();

        services.AddScoped<PlatformSettings>();
        services.AddScoped<ITaxCalculator, SettingsTaxCalculator>();
        services.AddScoped<ICommissionPolicy, CommissionPolicy>();
        services.AddScoped<IOrderPolicy, OrderPolicy>();

        return services;
    }
}
