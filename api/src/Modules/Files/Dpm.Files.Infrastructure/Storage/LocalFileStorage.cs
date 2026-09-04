using System.Security.Cryptography;
using Dpm.Files.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Dpm.Files.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Root of the local object store. Replaced by S3/Blob in deployed environments.</summary>
    public string RootPath { get; init; } = "./.data/files";

    public string QuarantineBucket { get; init; } = "dpm-quarantine";

    public string CleanBucket { get; init; } = "dpm-files";
}

/// <summary>
/// Filesystem-backed <see cref="IFileStorage"/> for local development and tests.
/// It keeps the same two-bucket shape as the deployed S3-compatible store, so
/// promoting a scanned file behaves identically in both.
/// </summary>
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly FileStorageOptions _options = options.Value;

    public async Task WritePartAsync(string quarantineKey, int partNumber, Stream content, CancellationToken ct)
    {
        var directory = PartDirectory(quarantineKey);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, PartFileName(partNumber));
        await using var target = File.Create(path);
        await content.CopyToAsync(target, ct);
    }

    public async Task<AssembledFile> AssembleAsync(string quarantineKey, int totalParts, CancellationToken ct)
    {
        var directory = PartDirectory(quarantineKey);
        var assembledPath = AssembledPath(quarantineKey);
        Directory.CreateDirectory(Path.GetDirectoryName(assembledPath)!);

        long size;
        byte[] hash;
        await using (var assembled = File.Create(assembledPath))
        {
            using var sha = SHA256.Create();
            await using var hashing = new CryptoStream(assembled, sha, CryptoStreamMode.Write);

            for (var part = 1; part <= totalParts; part++)
            {
                var partPath = Path.Combine(directory, PartFileName(part));
                await using var source = File.OpenRead(partPath);
                await source.CopyToAsync(hashing, ct);
            }

            await hashing.FlushFinalBlockAsync(ct);
            size = assembled.Length;
            hash = sha.Hash!;
        }

        return new AssembledFile(size, Convert.ToHexString(hash).ToLowerInvariant());
    }

    public Task<Stream> OpenQuarantinedAsync(string quarantineKey, CancellationToken ct) =>
        Task.FromResult<Stream>(File.OpenRead(AssembledPath(quarantineKey)));

    public Task<string> PromoteToCleanAsync(string quarantineKey, CancellationToken ct)
    {
        var cleanKey = quarantineKey.StartsWith("quarantine/", StringComparison.Ordinal)
            ? string.Concat("clean/", quarantineKey.AsSpan("quarantine/".Length))
            : $"clean/{quarantineKey}";

        var destination = Path.Combine(_options.RootPath, _options.CleanBucket, cleanKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(AssembledPath(quarantineKey), destination, overwrite: true);

        var partDirectory = PartDirectory(quarantineKey);
        if (Directory.Exists(partDirectory))
        {
            Directory.Delete(partDirectory, recursive: true);
        }

        return Task.FromResult(cleanKey);
    }

    public Task DeleteQuarantinedAsync(string quarantineKey, CancellationToken ct)
    {
        var assembled = AssembledPath(quarantineKey);
        if (File.Exists(assembled))
        {
            File.Delete(assembled);
        }

        var partDirectory = PartDirectory(quarantineKey);
        if (Directory.Exists(partDirectory))
        {
            Directory.Delete(partDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private string PartDirectory(string quarantineKey) =>
        Path.Combine(_options.RootPath, _options.QuarantineBucket, quarantineKey, "parts");

    private string AssembledPath(string quarantineKey) =>
        Path.Combine(_options.RootPath, _options.QuarantineBucket, quarantineKey, "assembled.bin");

    private static string PartFileName(int partNumber) => $"{partNumber:D6}.part";
}
