using Dpm.Downloads.Application;
using Dpm.Downloads.Application.Abstractions;
using Dpm.Downloads.Infrastructure;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Downloads.Api;

public static class DownloadsModule
{
    public static IServiceCollection AddDownloadsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DownloadOptions>(configuration.GetSection(DownloadOptions.SectionName));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(IssueDownloadUrlQuery).Assembly);
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(IssueDownloadUrlQuery).Assembly);

        services.AddScoped<ISignedUrlFactory, SignedUrlFactory>();
        services.AddScoped<IDownloadLog, DownloadLog>();
        services.AddScoped<ICurrentDownloader, CurrentDownloader>();

        return services;
    }
}
