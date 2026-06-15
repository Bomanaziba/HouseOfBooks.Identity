namespace HouseOfBooks.Identity.Application.Abstractions;

/// <summary>
/// Factory that creates a fresh IUnitOfWork per operation.
/// Allows the orchestrator to remain stateless.
/// </summary>
public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> CreateAsync(CancellationToken ct = default);
}
