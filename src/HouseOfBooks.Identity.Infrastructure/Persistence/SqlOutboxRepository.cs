namespace HouseOfBooks.Identity.Infrastructure.Persistence;

using System.Data;
using Dapper;
using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Events;

public sealed class SqlOutboxRepository : IOutboxRepository
{
    private readonly SqlServerUnitOfWork _uow;

    public SqlOutboxRepository(IUnitOfWork uow)
    {
        _uow = (SqlServerUnitOfWork)uow;
    }

    public async Task InsertAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@OutboxId",     outboxEvent.OutboxId,     DbType.Guid);
        parameters.Add("@AggregateId",  outboxEvent.AggregateId,  DbType.Guid);
        parameters.Add("@EventType",    outboxEvent.EventType,     DbType.String, size: 100);
        parameters.Add("@Payload",      outboxEvent.Payload,       DbType.String);
        parameters.Add("@CreatedAtUtc", outboxEvent.CreatedAtUtc,  DbType.DateTime2);

        var cmd = new CommandDefinition(
            commandText:   "usp_Identity_InsertOutboxEvent",
            parameters:    parameters,
            transaction:   _uow.Transaction,   // enlisted — committed or rolled back
            commandType:   CommandType.StoredProcedure,
            cancellationToken: ct);

        await _uow.Connection.ExecuteAsync(cmd);
    }
}