
namespace HouseOfBooks.Identity.Infrastructure.Persistence;

using System.Data;
using Dapper;
using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Models;

public sealed class SqlExternalIdentityRepository : IExternalIdentityService
{
    private readonly SqlServerUnitOfWork _uow;

    public SqlExternalIdentityRepository(IUnitOfWork uow)
    {
        _uow = (SqlServerUnitOfWork)uow;
    }

    public async Task<Guid> PersistMappingAsync(
        ExternalIdentityMapping mapping,
        CancellationToken ct = default)
    {
        var mappingId = Guid.NewGuid();

        var parameters = new DynamicParameters();
        parameters.Add("@MappingId",          mappingId,                    DbType.Guid);
        parameters.Add("@UserId",             mapping.UserId,               DbType.Guid);
        parameters.Add("@SchoolId",           mapping.SchoolId,             DbType.Guid);
        parameters.Add("@Role",               mapping.Role.ToString(),      DbType.String, size: 50);
        parameters.Add("@ExternalIdentifier", mapping.ExternalIdentifier,   DbType.String, size: 200);
        parameters.Add("@CreatedAtUtc",       DateTime.UtcNow,              DbType.DateTime2);

        var cmd = new CommandDefinition(
            commandText: "usp_Identity_PersistExternalMapping",
            parameters: parameters,
            transaction: _uow.Transaction,   // same ambient transaction
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        await _uow.Connection.ExecuteAsync(cmd);
        return mappingId;
    }

    public async Task<bool> MappingExistsAsync(
        Guid schoolId,
        string externalIdentifier,
        CancellationToken ct = default)
    {
        // Read-only — uses its own short-lived connection, NOT the ambient UoW
        // (this is called BEFORE the transaction opens)
        var connStr = _uow.Connection.ConnectionString; // borrow conn string only

        await using var readConn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
        await readConn.OpenAsync(ct);

        var parameters = new DynamicParameters();
        parameters.Add("@SchoolId",           schoolId,           DbType.Guid);
        parameters.Add("@ExternalIdentifier", externalIdentifier, DbType.String, size: 200);

        var cmd = new CommandDefinition(
            commandText: "usp_Identity_CheckExternalMappingExists",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var count = await readConn.ExecuteScalarAsync<int>(cmd);
        return count > 0;
    }
}
