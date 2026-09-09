using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Outbox;

public static class OutboxModule
{
    public static IServiceCollection AddOutboxDispatcher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<OutboxDbContext>(builder =>
            builder.UseSqlServer(configuration.GetConnectionString("Database")));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
