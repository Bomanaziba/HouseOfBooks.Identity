namespace HouseOfBooks.Identity.Infrastructure.Persistence;

// Options record wired via appsettings.json
public sealed class IdentityPersistenceOptions
{
    public const string Section = "Identity:Persistence";
    public required string ConnectionString { get; init; }
}
