using HouseOfBooks.Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace HouseOfBooks.Identity.Infrastructure.Outbox;



public sealed class OutboxEventProcessor : IOutboxEventProcessor
{
    private readonly IOutboxRelayRepository _relayRepo;
    private readonly ILogger<OutboxEventProcessor> _logger;

    public OutboxEventProcessor(
        IOutboxRelayRepository relayRepo,
        ILogger<OutboxEventProcessor> logger)
    {
        _relayRepo = relayRepo;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken ct = default)
    {
        var pending = await _relayRepo.FetchUnprocessedAsync(batchSize: 50, ct);

        foreach (var evt in pending)
        {
            try
            {
                await DispatchAsync(evt, ct);
                await _relayRepo.MarkProcessedAsync(evt.OutboxId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process outbox event {OutboxId} ({EventType})",
                    evt.OutboxId, evt.EventType);
                // Leave unprocessed — will retry next batch
            }
        }
    }

    private Task DispatchAsync(OutboxEvent evt, CancellationToken ct)
    {
        // Current: log only. Future: publish to event bus / send email / etc.
        _logger.LogInformation(
            "Dispatching outbox event {EventType} for aggregate {AggregateId}",
            evt.EventType, evt.AggregateId);

        // Example future hook:
        // if (evt.EventType == nameof(UserCreatedEvent))
        // {
        //     var payload = JsonSerializer.Deserialize<UserCreatedEvent>(evt.Payload)!;
        //     await _eventBus.PublishAsync(payload, ct);
        // }

        return Task.CompletedTask;
    }
}
