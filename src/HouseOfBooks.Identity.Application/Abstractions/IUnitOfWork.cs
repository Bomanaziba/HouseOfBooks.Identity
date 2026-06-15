namespace HouseOfBooks.Identity.Application.Abstractions;

/// <summary>
/// Unit-of-work abstraction that wraps an ambient DB transaction.
/// Orchestration layer starts, commits, or rolls back here.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
