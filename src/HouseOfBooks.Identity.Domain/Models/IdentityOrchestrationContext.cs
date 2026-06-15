
namespace HouseOfBooks.Identity.Domain.Models;

public sealed record IdentityOrchestrationContext
{
    public required CreateUserRequest Request { get; init; }
    public Guid UserId { get; set; }
    public string? ResolvedIdentity { get; set; }
    public bool IsMigrationMode => Request.ExternalIdentifier is not null;
}
