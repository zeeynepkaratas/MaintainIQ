using System.Data;
using MaintainIQ.Data.Common;
using MaintainIQ.Data.Connection;
using Microsoft.Data.SqlClient;

namespace MaintainIQ.Data.Transactions;

public sealed class UnitOfWork(IDbConnectionFactory connectionFactory) : IUnitOfWork
{
    private SqlConnection? _connection;
    private SqlTransaction? _transaction;
    private bool _disposed;

    public SqlConnection Connection => _connection
        ?? throw new UnitOfWorkException("Unit of Work başlatılmadı.");

    public SqlTransaction Transaction => _transaction
        ?? throw new UnitOfWorkException("Aktif transaction bulunamadı.");

    public bool IsActive => _transaction is not null;

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsActive)
        {
            throw new UnitOfWorkException("Transaction zaten aktif.");
        }

        _connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        _transaction = (SqlTransaction)await _connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureActive();
        await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await CleanupAsync().ConfigureAwait(false);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (!IsActive)
        {
            return;
        }

        await Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        await CleanupAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (IsActive)
        {
            Transaction.Rollback();
        }

        _transaction?.Dispose();
        _connection?.Dispose();
        _transaction = null;
        _connection = null;
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (IsActive)
        {
            await Transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await CleanupAsync().ConfigureAwait(false);
        _disposed = true;
    }

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsActive)
        {
            throw new UnitOfWorkException("Aktif transaction bulunamadı.");
        }
    }

    private async Task CleanupAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _transaction = null;
        _connection = null;
    }
}
