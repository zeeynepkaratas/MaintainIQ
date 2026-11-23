using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using MaintainIQ.Services.WorkOrders;

namespace MaintainIQ.Services.Failures;

public sealed record ReportFailureCommand(int EquipmentId, int CauseId, FailureSeverity Severity, string Description);

public interface IFailureService
{
    Task<int> ReportAsync(ReportFailureCommand command, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(int failureId, CancellationToken cancellationToken = default);
    Task ResolveAsync(int failureId, CancellationToken cancellationToken = default);
    Task<int> CreateWorkOrderAsync(
        int failureId,
        Priority requestedPriority,
        CancellationToken cancellationToken = default);
}

public sealed class FailureService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IClock clock,
    IWorkOrderNumberGenerator numberGenerator) : IFailureService
{
    private readonly AuditWriter _audit = new(currentUser);

    public Task<int> ReportAsync(ReportFailureCommand command, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ReportFailure);
        return InTransactionAsync(async session =>
        {
            var equipment = await session.Equipment.GetByIdAsync(command.EquipmentId, cancellationToken).ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(Domain.Entities.Equipment), command.EquipmentId);
            var now = clock.UtcNow;
            var failure = new Failure(
                command.EquipmentId, command.CauseId, command.Severity,
                command.Description, currentUser.PersonId, now);
            var id = await session.Failures.AddAsync(failure, cancellationToken).ConfigureAwait(false);

            if (command.Severity == FailureSeverity.Critical
                && equipment.Status != EquipmentStatus.Faulty)
            {
                equipment.MarkFaulty();
                equipment.MarkUpdated(now);
                await session.Equipment.UpdateAsync(equipment, cancellationToken).ConfigureAwait(false);
            }

            await _audit.WriteAsync(
                session, nameof(Failure), id.ToString(), "Reported", null,
                new { command.EquipmentId, command.CauseId, command.Severity, command.Description },
                now, cancellationToken).ConfigureAwait(false);
            return id;
        }, cancellationToken);
    }

    public Task AcknowledgeAsync(int failureId, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageFailure);
        return MutateAsync(failureId, "Acknowledged", (failure, now) => failure.Acknowledge(now), cancellationToken);
    }

    public Task ResolveAsync(int failureId, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageFailure);
        return MutateAsync(failureId, "Resolved", (failure, now) =>
        {
            failure.Resolve(now);
            failure.MarkUpdated(now);
        }, cancellationToken);
    }

    public Task<int> CreateWorkOrderAsync(
        int failureId,
        Priority requestedPriority,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.CreateWorkOrderFromFailure);
        return InTransactionAsync(async session =>
        {
            var failure = await RequireAsync(session, failureId, cancellationToken).ConfigureAwait(false);
            _ = await session.Equipment.GetByIdAsync(failure.EquipmentId, cancellationToken).ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(Domain.Entities.Equipment), failure.EquipmentId);
            var priority = failure.Severity == FailureSeverity.Critical && requestedPriority < Priority.High
                ? Priority.High
                : requestedPriority;
            var now = clock.UtcNow;
            var order = new CorrectiveWorkOrder(
                numberGenerator.Next(now), failure.EquipmentId, failure.Id, priority,
                failure.Description, now);
            var id = await session.WorkOrders.AddAsync(order, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(WorkOrder), id.ToString(), "CreatedFromFailure", null,
                new { FailureId = failure.Id, order.EquipmentId, order.Number, order.Priority },
                now, cancellationToken).ConfigureAwait(false);
            return id;
        }, cancellationToken);
    }

    private Task MutateAsync(
        int id,
        string action,
        Action<Failure, DateTimeOffset> mutate,
        CancellationToken cancellationToken) =>
        InTransactionAsync(async session =>
        {
            var failure = await RequireAsync(session, id, cancellationToken).ConfigureAwait(false);
            var oldValues = new { failure.Status, failure.ResolvedAt };
            var now = clock.UtcNow;
            mutate(failure, now);
            await session.Failures.UpdateAsync(failure, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Failure), failure.Id.ToString(), action,
                oldValues, new { failure.Status, failure.ResolvedAt }, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    private static async Task<Failure> RequireAsync(
        IServiceDataSession session,
        int id,
        CancellationToken cancellationToken) =>
        await session.Failures.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException(nameof(Failure), id);

    private async Task InTransactionAsync(
        Func<IServiceDataSession, Task> action,
        CancellationToken cancellationToken) =>
        await InTransactionAsync(async session =>
        {
            await action(session).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);

    private async Task<T> InTransactionAsync<T>(
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
}
