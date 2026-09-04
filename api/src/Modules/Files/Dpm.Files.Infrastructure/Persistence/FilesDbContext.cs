using Dpm.Files.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Files.Infrastructure.Persistence;

public sealed class FilesDbContext(DbContextOptions<FilesDbContext> options) : DbContext(options)
{
    public DbSet<FileUpload> FileUploads => Set<FileUpload>();

    public DbSet<ProductFile> ProductFiles => Set<ProductFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FilesDbContext).Assembly);
}
