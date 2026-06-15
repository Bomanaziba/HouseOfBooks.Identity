namespace HouseOfBooks.Identity.Application.Abstractions;

public interface IOutboxRelayRepository
{
    Task<IReadOnlyList<OutboxEvent>> FetchUnprocessedAsync(
        int batchSize, CancellationToken ct = default);

    Task MarkProcessedAsync(Guid outboxId, CancellationToken ct = default);
}