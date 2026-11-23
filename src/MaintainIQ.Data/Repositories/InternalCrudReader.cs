using Dapper;
using MaintainIQ.Data.Transactions;

namespace MaintainIQ.Data.Repositories;

internal sealed class InternalCrudReader<TEntity, TRow>(
    IUnitOfWork unitOfWork,
    Func<TRow, TEntity> mapper,
    string selectByIdSql,
    string selectAllSql)
    where TEntity : class
{
    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            selectByIdSql, new { Id = id }, unitOfWork.Transaction, cancellationToken: cancellationToken);
        var row = await unitOfWork.Connection.QuerySingleOrDefaultAsync<TRow>(command).ConfigureAwait(false);
        return row is null ? null : mapper(row);
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            selectAllSql, transaction: unitOfWork.Transaction, cancellationToken: cancellationToken);
        var rows = await unitOfWork.Connection.QueryAsync<TRow>(command).ConfigureAwait(false);
        return rows.Select(mapper).ToArray();
    }
}
