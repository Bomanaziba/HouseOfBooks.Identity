namespace HouseOfBooks.Identity.Infrastructure.Persistence;

using System.Data;
using Dapper;
using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Events;
using HouseOfBooks.Identity.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

public sealed class SqlOutboxRelayRepository : IOutboxRelayRepository
{
    private readonly string _connectionString;

    public SqlOutboxRelayRepository(IOptions<IdentityPersistenceOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IReadOnlyList<OutboxEvent>> FetchUnprocessedAsync(
        int batchSize,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var parameters = new DynamicParameters();
        parameters.Add("@BatchSize", batchSize, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText:   "usp_Identity_FetchUnprocessedOutboxEvents",
            parameters:    parameters,
            commandType:   CommandType.StoredProcedure,
            cancellationToken: ct);

        var rows = await conn.QueryAsync<OutboxEventRow>(cmd);

        return rows.Select(r => new OutboxEvent
        {
            OutboxId     = r.OutboxId,
            AggregateId  = r.AggregateId,
            EventType    = r.EventType,
            Payload      = r.Payload,
            CreatedAtUtc = r.CreatedAtUtc
        }).ToList();
    }

    public async Task MarkProcessedAsync(Guid outboxId, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var parameters = new DynamicParameters();
        parameters.Add("@OutboxId",       outboxId,        DbType.Guid);
        parameters.Add("@ProcessedAtUtc", DateTime.UtcNow, DbType.DateTime2);

        var cmd = new CommandDefinition(
            commandText:   "usp_Identity_MarkOutboxEventProcessed",
            parameters:    parameters,
            commandType:   CommandType.StoredProcedure,
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
    }

    // Private Dapper projection — never leaves this class
    private sealed record OutboxEventRow(
        Guid     OutboxId,
        Guid     AggregateId,
        string   EventType,
        string   Payload,
        DateTime CreatedAtUtc);
}