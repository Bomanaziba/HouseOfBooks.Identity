using HouseOfBooks.Identity.Domain.Enums;

namespace HouseOfBooks.Identity.Domain.Models;

public sealed record CreateUserRequest
{
    public required Guid SchoolId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required RoleCategory Role { get; init; }

    // When provided, system enters Migration Mode:
    // the external identifier is used as the user's identity
    // and a mapping record is persisted.
    public string? ExternalIdentifier { get; init; }

    // Optional: additional context consumed by the identity
    // generator (e.g., department code, cohort, class arm)
    public Dictionary<string, string>? IdentityContext { get; init; }

    /// <summary>
    /// Caller-supplied idempotency key (e.g. UUID v4).
    /// If a request with this key already succeeded, the original
    /// result is returned without re-executing the workflow.
    /// </summary>
    public string? IdempotencyKey { get; init; }

}