using HouseOfBooks.Identity.Domain.Enums;

namespace HouseOfBooks.Identity.Domain.Models;

public sealed record ExternalIdentityMapping
{
    public required Guid UserId { get; init; }
    public required Guid SchoolId { get; init; }
    public required RoleCategory Role { get; init; }
    public required string ExternalIdentifier { get; init; }
}
