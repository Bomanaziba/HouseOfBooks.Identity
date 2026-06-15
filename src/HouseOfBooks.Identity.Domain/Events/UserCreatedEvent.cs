namespace HouseOfBooks.Identity.Domain.Events;

public sealed record UserCreatedEvent(
    Guid UserId,
    Guid SchoolId,
    string AssignedIdentity,
    string Role,
    bool IsMigrated,
    string? ExternalIdentifier,
    DateTime OccurredAtUtc);