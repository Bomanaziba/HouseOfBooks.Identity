using HouseOfBooks.Identity.Domain.Enums;

namespace HouseOfBooks.Identity.Application.Abstractions;

/// <summary>
/// Generates a final, formatted identity string for a user.
/// Internally coordinates format resolution, sequencing,
/// and formatting. Callers must not replicate that logic.
/// </summary>
public interface IIdentityGeneratorService
{
    /// <param name="externalIdentifier">
    /// When supplied the generator returns it as-is after
    /// validation — the caller is in migration mode.
    /// </param>
    Task<string> GenerateAsync(
        Guid schoolId,
        RoleCategory role,
        Dictionary<string, string>? context = null,
        string? externalIdentifier = null,
        CancellationToken ct = default);
}