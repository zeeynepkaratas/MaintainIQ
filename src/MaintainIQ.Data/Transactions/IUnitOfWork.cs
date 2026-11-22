using Microsoft.Data.SqlClient;

namespace MaintainIQ.Data.Transactions;

public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    SqlConnection Connection { get; }
    SqlTransaction Transaction { get; }
    bool IsActive { get; }
    Task BeginAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
