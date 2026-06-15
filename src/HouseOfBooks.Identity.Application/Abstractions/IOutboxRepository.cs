
namespace HouseOfBooks.Identity.Application.Abstractions;

public interface IOutboxRepository
{
    /// <summary>
    /// Inserts an outbox event WITHIN the ambient UoW transaction.
    /// Atomicity is guaranteed: the event is committed only if
    /// the user record is also committed.
    /// </summary>
    Task InsertAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
}

public sealed record OutboxEvent
{
    public Guid OutboxId { get; init; } = Guid.NewGuid();
    public required Guid AggregateId { get; init; }   // UserId
    public required string EventType { get; init; }
    public required string Payload { get; init; }     // JSON
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}