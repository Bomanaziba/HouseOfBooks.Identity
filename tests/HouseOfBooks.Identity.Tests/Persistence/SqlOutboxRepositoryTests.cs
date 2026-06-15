
namespace HouseOfBooks.Identity.Tests.Persistence;

using Dapper;
using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Events;
using HouseOfBooks.Identity.Infrastructure.Persistence;
using HouseOfBooks.Identity.Tests.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

[Collection("Database")]   // serialised against other DB tests via shared fixture
public sealed class SqlOutboxRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _db;
    private SqlServerUnitOfWork _uow = null!;

    public SqlOutboxRepositoryTests(DatabaseFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        _uow = new SqlServerUnitOfWork(
            _db.ConnectionString,
            Microsoft.Extensions.Logging.Abstractions
                     .NullLogger<SqlServerUnitOfWork>.Instance);
        await _uow.BeginAsync();
    }

    public async Task DisposeAsync()
    {
        // Always roll back integration tests — keep DB clean
        await _uow.RollbackAsync();
        await _uow.DisposeAsync();
    }

    [Fact]
    public async Task InsertAsync_WithinUow_RowVisibleBeforeCommit()
    {
        var repo = new SqlOutboxRepository(_uow);
        var evt  = new OutboxEvent
        {
            OutboxId    = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            EventType   = "UserCreatedEvent",
            Payload     = """{"test":true}"""
        };

        await repo.InsertAsync(evt);

        // Read within the same connection/transaction — row must be visible
        var options      = Options.Create(new IdentityPersistenceOptions
        {
            ConnectionString = _db.ConnectionString
        });
        var relayRepo    = new SqlOutboxRelayRepository(options);

        // Note: relay opens its own connection with READ COMMITTED —
        // the row will NOT be visible until committed. This is expected.
        // We verify existence via the same ambient connection instead.
        var count = await _uow.Connection.ExecuteScalarAsync<int>(
            new Dapper.CommandDefinition(
                "SELECT COUNT(1) FROM Identity.Outbox WHERE OutboxId = @Id",
                new { Id = evt.OutboxId },
                transaction: _uow.Transaction));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MarkProcessedAsync_SetsProcessedAtUtc()
    {
        // Seed a committed row via relay repo's own connection
        var outboxId    = Guid.NewGuid();
        var connStr     = _db.ConnectionString;

        await using (var seedConn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
        {
            await seedConn.OpenAsync();
            await seedConn.ExecuteAsync(
                "INSERT INTO Identity.Outbox " +
                "(OutboxId, AggregateId, EventType, Payload, CreatedAtUtc) " +
                "VALUES (@OutboxId, @AggId, @Type, @Payload, @Now)",
                new
                {
                    OutboxId  = outboxId,
                    AggId     = Guid.NewGuid(),
                    Type      = "UserCreatedEvent",
                    Payload   = "{}",
                    Now       = DateTime.UtcNow
                });
        }

        var options   = Options.Create(new IdentityPersistenceOptions
        {
            ConnectionString = connStr
        });
        var relayRepo = new SqlOutboxRelayRepository(options);

        await relayRepo.MarkProcessedAsync(outboxId);

        // Verify
        await using var verifyConn =
            new Microsoft.Data.SqlClient.SqlConnection(connStr);
        await verifyConn.OpenAsync();

        var processedAt = await verifyConn.ExecuteScalarAsync<DateTime?>(
            "SELECT ProcessedAtUtc FROM Identity.Outbox WHERE OutboxId = @Id",
            new { Id = outboxId });

        Assert.NotNull(processedAt);
        await verifyConn.ExecuteAsync(
            "DELETE FROM Identity.Outbox WHERE OutboxId = @Id",
            new { Id = outboxId });
    }
}

