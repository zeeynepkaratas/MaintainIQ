using MaintainIQ.Data.ReadModels;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.ValueObjects;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Services.Technicians;

public sealed record CreateTechnicianCommand(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? Specialty = null);

public interface ITechnicianService
{
    Task<int> CreateAsync(CreateTechnicianCommand command, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, string firstName, string lastName, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Technician>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrderListItemDto>> GetWorkloadAsync(int technicianId, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(int technicianId, DateRange window, CancellationToken cancellationToken = default);
}

public sealed class TechnicianService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IClock clock) : ITechnicianService
{
    private readonly AuditWriter _audit = new(currentUser);

    public Task<int> CreateAsync(CreateTechnicianCommand command, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.AssignWorkOrder);
        return InTransactionAsync(async session =>
        {
            var now = clock.UtcNow;
            var technician = new Technician(
                command.EmployeeNumber, command.FirstName, command.LastName, now, command.Specialty);
            var id = await session.Technicians.AddAsync(technician, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Technician), id.ToString(), "Created", null,
                new { command.EmployeeNumber, command.FirstName, command.LastName, command.Specialty },
                now, cancellationToken).ConfigureAwait(false);
            return id;
        }, cancellationToken);
    }

    public Task UpdateAsync(
        int id,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.AssignWorkOrder);
        return InTransactionAsync(async session =>
        {
            var technician = await RequireAsync(session, id, cancellationToken).ConfigureAwait(false);
            var oldValues = new { technician.FirstName, technician.LastName };
            var now = clock.UtcNow;
            technician.UpdateProfile(firstName, lastName, now);
            await session.Technicians.UpdateAsync(technician, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Technician), id.ToString(), "Updated", oldValues,
                new { technician.FirstName, technician.LastName }, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.AssignWorkOrder);
        return InTransactionAsync(async session =>
        {
            var technician = await RequireAsync(session, id, cancellationToken).ConfigureAwait(false);
            technician.Deactivate();
            var now = clock.UtcNow;
            technician.MarkUpdated(now);
            await session.Technicians.DeleteAsync(technician, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Technician), id.ToString(), "Deactivated",
                new { IsActive = true }, new { technician.IsActive }, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Technician>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var result = await session.Technicians.GetAllAsync(cancellationToken).ConfigureAwait(false);
        await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<WorkOrderListItemDto>> GetWorkloadAsync(
        int technicianId,
        CancellationToken cancellationToken = default)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var result = await session.WorkOrders.SearchAsync(
            new WorkOrderFilter(TechnicianId: technicianId, PageSize: 200), cancellationToken).ConfigureAwait(false);
        await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<bool> IsAvailableAsync(
        int technicianId,
        DateRange window,
        CancellationToken cancellationToken = default)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var conflict = await session.WorkOrders.HasScheduleConflictAsync(
            technicianId, window, null, cancellationToken).ConfigureAwait(false);
        await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return !conflict;
    }

    private static async Task<Technician> RequireAsync(
        IServiceDataSession session,
        int id,
        CancellationToken cancellationToken) =>
        await session.Technicians.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException(nameof(Technician), id);

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
