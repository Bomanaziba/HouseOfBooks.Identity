// ═════════════════════════════════════════════════════════════════
// FILE: API/Responses/CreateUserHttpResponse.cs
// Outbound DTO — what the HTTP caller receives on success.
// ═════════════════════════════════════════════════════════════════

namespace HouseOfBooks.Identity.API.Responses;

public sealed record CreateUserHttpResponse
{
    public required Guid   UserId           { get; init; }
    public required string AssignedIdentity { get; init; }
    public required bool   IsMigrated       { get; init; }
    public Guid?           ExternalMappingId { get; init; }
}