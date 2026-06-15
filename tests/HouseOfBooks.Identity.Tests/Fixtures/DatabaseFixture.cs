namespace HouseOfBooks.Identity.Tests.Fixtures;

using Xunit;

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

public sealed class DatabaseFixture
{
    // Pull from env var so CI pipeline injects the right connection string.
    // Falls back to local dev default.
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("IDENTITY_TEST_DB")
        ?? "Server=localhost;Database=HouseOfBooks_Identity_Test;" +
           "Trusted_Connection=True;TrustServerCertificate=True;";
}