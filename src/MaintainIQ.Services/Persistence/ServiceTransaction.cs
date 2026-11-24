namespace MaintainIQ.Services.Persistence;

/// <summary>Bir kullanım senaryosunu tek Unit of Work transaction'ı içinde çalıştırır.</summary>
internal static class ServiceTransaction
{
    public static async Task<T> RunAsync<T>(
        IServiceDataSessionFactory sessionFactory,
        Func<IServiceDataSession, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await action(session).ConfigureAwait(false);
            await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await session.UnitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public static Task RunAsync(
        IServiceDataSessionFactory sessionFactory,
        Func<IServiceDataSession, Task> action,
        CancellationToken cancellationToken) =>
        RunAsync<bool>(sessionFactory, async session =>
        {
            await action(session).ConfigureAwait(false);
            return true;
        }, cancellationToken);
}
