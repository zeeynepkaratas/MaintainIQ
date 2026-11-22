using Dapper;
using MaintainIQ.Data.Common;
using MaintainIQ.Data.Transactions;

namespace MaintainIQ.Data.Repositories;

public abstract class Repository<T>(IUnitOfWork unitOfWork) : IRepository<T> where T : class
{
    protected IUnitOfWork UnitOfWork { get; } = unitOfWork;

    protected CommandDefinition Command(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default) =>
        new(sql, parameters, UnitOfWork.Transaction, cancellationToken: cancellationToken);

    protected static void EnsureUpdated(int affectedRows, string entityName)
    {
        if (affectedRows == 0)
        {
            throw new ConcurrencyException($"{entityName} başka bir kullanıcı tarafından değiştirilmiş veya silinmiş.");
        }
    }

    public abstract Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    public abstract Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    public abstract Task<int> AddAsync(T entity, CancellationToken cancellationToken = default);
    public abstract Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    public abstract Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}
