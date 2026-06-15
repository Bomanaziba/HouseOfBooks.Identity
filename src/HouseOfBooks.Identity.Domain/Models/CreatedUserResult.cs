namespace HouseOfBooks.Identity.Domain.Models;

public sealed record CreatedUserResult
{
    public required Guid UserId { get; init; }
    public required string AssignedIdentity { get; init; }
    public required bool IsMigrated { get; init; }
    public Guid? ExternalMappingId { get; init; }
}