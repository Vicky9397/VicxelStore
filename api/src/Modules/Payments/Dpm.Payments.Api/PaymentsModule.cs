using Dpm.Payments.Application.Abstractions;
using Dpm.Payments.Application.Intents;
using Dpm.Payments.Application.Webhooks;
using Dpm.Payments.Contracts;
using Dpm.Payments.Infrastructure.Persistence;
using Dpm.Payments.Infrastructure.Providers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Payments.Api;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PaymentsDbContext>(builder =>
            builder.UseSqlServer(configuration.GetConnectionString("Database")));

        services.Configure<PaymentProviderOptions>(configuration.GetSection(PaymentProviderOptions.SectionName));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(HandleWebhookCommand).Assembly);
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(HandleWebhookCommand).Assembly);

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentsUnitOfWork, PaymentsUnitOfWork>();
        services.AddScoped<IWebhookInbox, WebhookInbox>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IPaymentIntentService, CreatePaymentIntentService>();

        // Providers are singletons behind the factory; adding a hosted gateway
        // means registering its adapter here and nothing else.
        services.AddSingleton<IPaymentProvider, SandboxPaymentProvider>();
        services.AddSingleton<IPaymentProvider, ManualBankTransferProvider>();
        services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();

        return services;
    }
}
