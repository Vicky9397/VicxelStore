using System.Threading.Channels;
using Dpm.Files.Application.Abstractions;
using Dpm.Files.Application.Scan;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dpm.Files.Infrastructure.Scanning;

/// <summary>
/// In-process scan dispatcher. Scanning runs off the request thread so a large upload
/// does not block the response (spec 02 section 2.4 File Management). Hangfire
/// replaces this when durable retries are wired up; the queued work is
/// idempotent either way because a file is scanned only while Pending.
/// </summary>
public sealed class BackgroundScanDispatcher : IScanDispatcher
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid productFilePublicId) => _channel.Writer.TryWrite(productFilePublicId);

    internal IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed partial class FileScanWorker(
    BackgroundScanDispatcher dispatcher,
    IServiceScopeFactory scopeFactory,
    ILogger<FileScanWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var fileId in dispatcher.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new ScanFileCommand(fileId), stoppingToken);
                if (result.IsFailure)
                {
                    LogScanFailed(logger, fileId, result.Error.Code);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A scan failure must not take the worker down: the file simply
                // stays Pending and therefore undownloadable.
                LogScanThrew(logger, fileId, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scan of file {FileId} failed with {ErrorCode}")]
    private static partial void LogScanFailed(ILogger logger, Guid fileId, string errorCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan of file {FileId} threw")]
    private static partial void LogScanThrew(ILogger logger, Guid fileId, Exception exception);
}
