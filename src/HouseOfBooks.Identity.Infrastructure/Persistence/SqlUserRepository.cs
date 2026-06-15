
namespace HouseOfBooks.Identity.Infrastructure.Persistence;

using System.Data;
using Dapper;
using HouseOfBooks.Identity.Application.Abstractions;
using HouseOfBooks.Identity.Domain.Models;

/// <summary>
/// All writes go through stored procedures.
/// The repository enlists in the ambient UoW transaction —
/// it does NOT open its own connection.
/// </summary>
public sealed class SqlUserRepository : IUserRepository
{
    // Injected as scoped — same instance within one HTTP request / operation
    private readonly SqlServerUnitOfWork _uow;

    public SqlUserRepository(IUnitOfWork uow)
    {
        // Safe downcast: in this assembly, IUnitOfWork is always
        // SqlServerUnitOfWork. If that changes, the cast throws at
        // startup, not silently at runtime.
        _uow = (SqlServerUnitOfWork)uow;
    }

    public async Task<Guid> CreateAsync(UserRecord user, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@UserId",            user.Id,              DbType.Guid);
        parameters.Add("@SchoolId",          user.SchoolId,        DbType.Guid);
        parameters.Add("@FirstName",         user.FirstName,       DbType.String, size: 100);
        parameters.Add("@LastName",          user.LastName,        DbType.String, size: 100);
        parameters.Add("@Email",             user.Email,           DbType.String, size: 256);
        parameters.Add("@Role",              user.Role.ToString(), DbType.String, size: 50);
        parameters.Add("@AssignedIdentity",  user.AssignedIdentity,DbType.String, size: 100);
        parameters.Add("@CreatedAtUtc",      user.CreatedAtUtc,    DbType.DateTime2);

        var cmd = new CommandDefinition(
            commandText: "usp_Identity_CreateUser",
            parameters: parameters,
            transaction: _uow.Transaction,   // enlists in ambient transaction
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        await _uow.Connection.ExecuteAsync(cmd);
        return user.Id;
    }
}
