using HouseOfBooks.Identity.Domain.Models;

namespace HouseOfBooks.Identity.Application.Abstractions;

/// <summary>
/// Persistence contract for the User aggregate.
/// All writes go through here — no direct DB access elsewhere.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Inserts the user record within the ambient transaction.
    /// Returns the newly assigned UserId (DB-generated or GUID).
    /// </summary>
    Task<Guid> CreateAsync(
        UserRecord user,
        CancellationToken ct = default);
}
