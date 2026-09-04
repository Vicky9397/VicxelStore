using Dpm.Files.Application.Abstractions;
using Dpm.Files.Application.Upload;
using Dpm.Files.Infrastructure.Persistence;
using Dpm.Files.Infrastructure.Scanning;
using Dpm.Files.Infrastructure.Storage;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dpm.Files.Api;

public static class FilesModule
{
    public static IServiceCollection AddFilesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<FilesDbContext>(builder =>
            builder.UseSqlServer(configuration.GetConnectionString("Database")));

        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<VirusScannerOptions>(configuration.GetSection(VirusScannerOptions.SectionName));

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(InitUploadCommand).Assembly);
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Dpm.BuildingBlocks.Application.LoggingBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(InitUploadCommand).Assembly);

        services.AddScoped<IFileUploadRepository, FileUploadRepository>();
        services.AddScoped<IProductFileRepository, ProductFileRepository>();
        services.AddScoped<IFilesUnitOfWork, FilesUnitOfWork>();
        services.AddScoped<ICurrentUploader, CurrentUploader>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        var scannerHost = configuration.GetSection(VirusScannerOptions.SectionName)["Host"];
        if (string.IsNullOrWhiteSpace(scannerHost))
        {
            services.AddSingleton<IVirusScanner, DevelopmentVirusScanner>();
        }
        else
        {
            services.AddSingleton<IVirusScanner, ClamAvVirusScanner>();
        }

        services.AddSingleton<BackgroundScanDispatcher>();
        services.AddSingleton<IScanDispatcher>(sp => sp.GetRequiredService<BackgroundScanDispatcher>());
        services.AddHostedService<FileScanWorker>();

        return services;
    }
}
