namespace HouseOfBooks.Identity.Infrastructure.Persistence;

using System.Data;
using HouseOfBooks.Identity.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps a single SqlConnection + SqlTransaction.
/// Passed (via ambient scope) to repositories so they
/// enlist in the same transaction without being coupled to it.
/// </summary>
public sealed class SqlServerUnitOfWork : IUnitOfWork
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerUnitOfWork> _logger;

    private SqlConnection? _connection;
    private SqlTransaction? _transaction;
    private bool _disposed;

    public SqlServerUnitOfWork(
        string connectionString,
        ILogger<SqlServerUnitOfWork> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    // Exposed to repositories — they call sp via this connection/transaction
    public IDbConnection Connection =>
        _connection ?? throw new InvalidOperationException(
            "Unit of work has not been started. Call BeginAsync first.");

    public IDbTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException(
            "No active transaction. Call BeginAsync first.");

    public async Task BeginAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _connection = new SqlConnection(_connectionString);
        await _connection.OpenAsync(ct);
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        _logger.LogDebug("Transaction started.");
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _transaction!.CommitAsync(ct);
        _logger.LogDebug("Transaction committed.");
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;

        await _transaction.RollbackAsync(ct);
        _logger.LogDebug("Transaction rolled back.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_transaction is not null)
            await _transaction.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}