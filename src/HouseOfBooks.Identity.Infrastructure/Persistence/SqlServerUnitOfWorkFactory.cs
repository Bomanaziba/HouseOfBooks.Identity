namespace HouseOfBooks.Identity.Infrastructure.Persistence;

using HouseOfBooks.Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class SqlServerUnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerUnitOfWork> _uowLogger;

    public SqlServerUnitOfWorkFactory(
        IOptions<IdentityPersistenceOptions> options,
        ILogger<SqlServerUnitOfWork> uowLogger)
    {
        _connectionString = options.Value.ConnectionString;
        _uowLogger = uowLogger;
    }

    public Task<IUnitOfWork> CreateAsync(CancellationToken ct = default)
    {
        // Returns a NEW, independent UoW every call — never shared
        IUnitOfWork uow = new SqlServerUnitOfWork(_connectionString, _uowLogger);
        return Task.FromResult(uow);
    }
}

