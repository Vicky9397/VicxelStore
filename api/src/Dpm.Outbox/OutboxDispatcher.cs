using System.Text.Json;
using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dpm.Outbox;

/// <summary>
/// Delivers persisted domain events after the transaction that produced them
/// commits (spec 04 section 4.7). Delivery is at-least-once and retried until it
/// succeeds, which is what keeps the money path whole: a payment capture is
/// recorded in the same transaction as its event, so the ledger post cannot be
/// lost even if the handler fails at first (spec 03 section 3.6).
///
/// Handlers must therefore be idempotent. A message that keeps failing is parked
/// rather than dropped, so a break is visible instead of silent.
/// </summary>
public sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogBatchFailed(logger, exception);
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var pending = await dbContext.Messages
            .Where(m => m.ProcessedAtUtc == null && m.Attempts < _options.MaxAttempts)
            .OrderBy(m => m.Id)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            message.Attempts++;

            var eventType = Type.GetType(message.Type);
            if (eventType is null)
            {
                message.Error = $"Unknown event type: {message.Type}";
                LogUnknownType(logger, message.EventId, message.Type);
                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize(message.PayloadJson, eventType) is not IDomainEvent domainEvent)
                {
                    message.Error = "Payload did not deserialize to a domain event.";
                    continue;
                }

                await publisher.Publish(domainEvent, ct);
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception exception)
            {
                // Left unprocessed so the next tick retries it.
                message.Error = exception.Message;
                LogDeliveryFailed(logger, message.EventId, message.Attempts, exception);
            }
        }

        if (pending.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {EventId} names unknown type {TypeName}")]
    private static partial void LogUnknownType(ILogger logger, Guid eventId, string typeName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {EventId} failed on attempt {Attempts}")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid eventId, int attempts, Exception exception);
}
