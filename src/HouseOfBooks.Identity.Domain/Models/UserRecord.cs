using HouseOfBooks.Identity.Domain.Enums;

namespace HouseOfBooks.Identity.Domain.Models;

public sealed record UserRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid SchoolId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required RoleCategory Role { get; init; }
    public required string AssignedIdentity { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}