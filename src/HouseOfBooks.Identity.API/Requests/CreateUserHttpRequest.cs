// ═════════════════════════════════════════════════════════════════
// FILE: API/Requests/CreateUserHttpRequest.cs
// Inbound DTO — what the HTTP caller sends.
// Deliberately flat and JSON-friendly.
// Never bleeds into Application or Domain layers.
// ═════════════════════════════════════════════════════════════════

namespace HouseOfBooks.Identity.API.Requests;

using HouseOfBooks.Identity.Domain.Enums;

public sealed record CreateUserHttpRequest
{
    public required Guid   SchoolId   { get; init; }
    public required string FirstName  { get; init; }
    public required string LastName   { get; init; }
    public required string Email      { get; init; }
    public required string Role       { get; init; }   // string → parsed to RoleCategory

    /// <summary>
    /// Optional. When present, system enters migration mode.
    /// The value becomes the user's assigned identity and a
    /// mapping record is persisted.
    /// </summary>
    public string? ExternalIdentifier { get; init; }

    /// <summary>
    /// Optional key→value pairs forwarded to the identity
    /// generator (e.g. department code, class arm, cohort).
    /// </summary>
    public Dictionary<string, string>? IdentityContext { get; init; }
}
