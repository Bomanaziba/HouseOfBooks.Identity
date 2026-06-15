using HouseOfBooks.Identity.Domain.Models;

namespace HouseOfBooks.Identity.Application.Abstractions;

public interface IIdempotencyStore
{
    /// <summary>
    /// Returns the cached result if the key was already processed,
    /// null otherwise.
    /// </summary>
    Task<CreatedUserResult?> TryGetAsync(
        string key,
        CancellationToken ct = default);

    /// <summary>
    /// Persists the result against the key so future duplicate
    /// requests short-circuit immediately.
    /// </summary>
    Task SetAsync(
        string key,
        CreatedUserResult result,
        CancellationToken ct = default);
}
