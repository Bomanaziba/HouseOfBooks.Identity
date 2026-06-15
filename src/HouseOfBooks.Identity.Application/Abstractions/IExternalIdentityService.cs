using HouseOfBooks.Identity.Domain.Models;

namespace HouseOfBooks.Identity.Application.Abstractions;

/// <summary>
/// Persists and retrieves external-to-internal identity mappings.
/// Used exclusively in migration mode.
/// </summary>
public interface IExternalIdentityService
{
    Task<Guid> PersistMappingAsync(
        ExternalIdentityMapping mapping,
        CancellationToken ct = default);

    Task<bool> MappingExistsAsync(
        Guid schoolId,
        string externalIdentifier,
        CancellationToken ct = default);
}