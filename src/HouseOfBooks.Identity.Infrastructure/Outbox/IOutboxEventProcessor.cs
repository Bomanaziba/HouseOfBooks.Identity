namespace HouseOfBooks.Identity.Infrastructure.Outbox;

public interface IOutboxEventProcessor
{
    Task ProcessPendingAsync(CancellationToken ct = default);
}
