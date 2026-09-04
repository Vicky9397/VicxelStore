using System.Net.Sockets;
using Dpm.Files.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dpm.Files.Infrastructure.Scanning;

public sealed class VirusScannerOptions
{
    public const string SectionName = "VirusScanner";

    /// <summary>ClamAV host. Empty means no scanner is configured for this environment.</summary>
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 3310;
}

/// <summary>
/// ClamAV client speaking the INSTREAM command. A file is treated as clean only
/// on an explicit OK; any other reply, including a scanner error, is treated as
/// not clean so an unscannable file never becomes downloadable.
/// </summary>
public sealed class ClamAvVirusScanner(IOptions<VirusScannerOptions> options) : IVirusScanner
{
    private const int ChunkSize = 64 * 1024;

    public async Task<bool> IsCleanAsync(Stream content, CancellationToken ct)
    {
        var settings = options.Value;
        using var client = new TcpClient();
        await client.ConnectAsync(settings.Host, settings.Port, ct);
        await using var network = client.GetStream();

        await network.WriteAsync("zINSTREAM\0"u8.ToArray(), ct);

        var buffer = new byte[ChunkSize];
        int read;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        {
            var length = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length, (uint)read);
            await network.WriteAsync(length, ct);
            await network.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        await network.WriteAsync(new byte[4], ct);
        await network.FlushAsync(ct);

        var response = new byte[256];
        var responseLength = await network.ReadAsync(response, ct);
        var reply = System.Text.Encoding.ASCII.GetString(response, 0, responseLength);
        return reply.Contains("OK", StringComparison.Ordinal)
            && !reply.Contains("FOUND", StringComparison.Ordinal);
    }
}

/// <summary>
/// Development stand-in used when no scanner host is configured. It passes every
/// file except the EICAR test string, which keeps the infected path exercisable
/// locally without shipping a real scanner.
/// </summary>
public sealed partial class DevelopmentVirusScanner(ILogger<DevelopmentVirusScanner> logger) : IVirusScanner
{
    private static readonly byte[] EicarMarker =
        System.Text.Encoding.ASCII.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE");

    public async Task<bool> IsCleanAsync(Stream content, CancellationToken ct)
    {
        LogNoScanner(logger);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        return bytes.AsSpan().IndexOf(EicarMarker) < 0;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No virus scanner configured; using the development scanner, which detects only the EICAR test file")]
    private static partial void LogNoScanner(ILogger logger);
}
