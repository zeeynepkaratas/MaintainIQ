using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Services.WorkOrders;

public interface IWorkOrderService
{
    Task AssignAsync(int id, int technicianId, DateRange window, bool approveConflict, CancellationToken cancellationToken = default);
    Task StartAsync(int id, CancellationToken cancellationToken = default);
    Task RequestPartsAsync(int id, string reason, CancellationToken cancellationToken = default);
    Task AddPartAsync(int id, int sparePartId, decimal quantity, CancellationToken cancellationToken = default);
    Task ResumeAsync(int id, CancellationToken cancellationToken = default);
    Task CompleteAsync(int id, int laborMinutes, CancellationToken cancellationToken = default);
    Task CloseAsync(int id, CancellationToken cancellationToken = default);
    Task ReopenAsync(int id, string reason, CancellationToken cancellationToken = default);
    Task CancelAsync(int id, string reason, CancellationToken cancellationToken = default);
}

public sealed class WorkOrderService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IClock clock) : IWorkOrderService
{
    private readonly AuditWriter _audit = new(currentUser);

    public Task AssignAsync(
        int id,
        int technicianId,
        DateRange window,
        bool approveConflict,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.AssignWorkOrder);
        return InTransactionAsync(async session =>
        {
            var order = await RequireAsync(session, id, false, cancellationToken).ConfigureAwait(false);
            var technician = await session.Technicians.GetByIdAsync(technicianId, cancellationToken).ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(Technician), technicianId);
            var conflict = await session.WorkOrders.HasScheduleConflictAsync(
                technicianId, window, id, cancellationToken).ConfigureAwait(false);
            var before = Snapshot(order);
            var now = clock.UtcNow;
            order.Assign(
                technicianId, technician.IsActive, window, conflict, approveConflict,
                currentUser.PersonId, now);
            await PersistTransitionAsync(session, order, "Assigned", before, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task StartAsync(int id, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ExecuteOwnWorkOrder);
        return MutateAsync(id, "Started", (order, now) =>
            order.Start(currentUser.PersonId, IsElevated, now), cancellationToken);
    }

    public Task RequestPartsAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ExecuteOwnWorkOrder);
        return MutateAsync(id, "WaitingForParts", (order, now) =>
            order.RequestParts(reason, currentUser.PersonId, now), cancellationToken);
    }

    public Task AddPartAsync(
        int id,
        int sparePartId,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ExecuteOwnWorkOrder);
        return InTransactionAsync(async session =>
        {
            var order = await RequireAsync(session, id, true, cancellationToken).ConfigureAwait(false);
            if (!IsElevated && order.AssignedTechnicianId != currentUser.PersonId)
            {
                throw new Domain.Common.PermissionDeniedException("Teknisyen yalnızca kendi iş emrine parça ekleyebilir.");
            }

            if (order.Parts.Any(item => item.SparePartId == sparePartId))
            {
                throw new Domain.Common.BusinessRuleException("WO-PART", "Aynı parça iş emrine yalnızca bir kez eklenebilir.");
            }

            var sparePart = await session.SpareParts.GetByIdAsync(sparePartId, cancellationToken).ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(SparePart), sparePartId);
            var now = clock.UtcNow;
            var part = new WorkOrderPart(sparePartId, quantity, sparePart.UnitCost, now);
            order.AddPart(part);
            await session.WorkOrders.AddPartAsync(order.Id, part, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(WorkOrder), order.Id.ToString(), "PartAdded", null,
                new { sparePartId, quantity, UnitCost = sparePart.UnitCost.Amount },
                now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task ResumeAsync(int id, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ExecuteOwnWorkOrder);
        return MutateAsync(id, "Resumed", (order, now) =>
            order.Resume(currentUser.PersonId, now), cancellationToken);
    }

    public Task CompleteAsync(int id, int laborMinutes, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ExecuteOwnWorkOrder);
        return InTransactionAsync(async session =>
        {
            var order = await RequireAsync(session, id, true, cancellationToken).ConfigureAwait(false);
            var before = Snapshot(order);
            var now = clock.UtcNow;

            foreach (var requestedPart in order.Parts.OrderBy(item => item.SparePartId))
            {
                var stockPart = await session.SpareParts.GetForUpdateAsync(
                    requestedPart.SparePartId, cancellationToken).ConfigureAwait(false)
                    ?? throw new EntityNotFoundException(nameof(SparePart), requestedPart.SparePartId);
                var unitCost = stockPart.Consume(requestedPart.Quantity);
                stockPart.MarkUpdated(now);
                await session.SpareParts.UpdateAsync(stockPart, cancellationToken).ConfigureAwait(false);
                await session.StockMovements.AddAsync(new StockMovement(
                    stockPart.Id,
                    StockMovementType.Consumption,
                    requestedPart.Quantity,
                    stockPart.StockQuantity,
                    unitCost,
                    now,
                    $"{order.Number} iş emri kullanımı",
                    order.Id), cancellationToken).ConfigureAwait(false);
            }

            order.Complete(laborMinutes, true, currentUser.PersonId, IsElevated, now);
            await session.WorkOrders.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
            await session.WorkOrders.AddStatusHistoryAsync(
                order.Id, order.StatusHistory.Last(), cancellationToken).ConfigureAwait(false);

            var equipment = await session.Equipment.GetByIdAsync(order.EquipmentId, cancellationToken).ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(Domain.Entities.Equipment), order.EquipmentId);
            if (equipment.Status is EquipmentStatus.Faulty or EquipmentStatus.UnderMaintenance)
            {
                equipment.MarkOperational();
                equipment.MarkUpdated(now);
                await session.Equipment.UpdateAsync(equipment, cancellationToken).ConfigureAwait(false);
            }

            await _audit.WriteAsync(
                session, nameof(WorkOrder), order.Id.ToString(), "Completed",
                before, Snapshot(order), now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task CloseAsync(int id, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.CloseReopenCancelWorkOrder);
        return MutateAsync(id, "Closed", (order, now) =>
            order.Close(currentUser.Role, currentUser.PersonId, now), cancellationToken);
    }

    public Task ReopenAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.CloseReopenCancelWorkOrder);
        return MutateAsync(id, "Reopened", (order, now) =>
            order.Reopen(reason, currentUser.Role, currentUser.PersonId, now), cancellationToken);
    }

    public Task CancelAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.CloseReopenCancelWorkOrder);
        return MutateAsync(id, "Cancelled", (order, now) =>
            order.Cancel(reason, currentUser.Role, currentUser.PersonId, now), cancellationToken);
    }

    private bool IsElevated => currentUser.Role is UserRole.Admin or UserRole.Supervisor;

    private Task MutateAsync(
        int id,
        string action,
        Action<WorkOrder, DateTimeOffset> mutation,
        CancellationToken cancellationToken) =>
        InTransactionAsync(async session =>
        {
            var order = await RequireAsync(session, id, false, cancellationToken).ConfigureAwait(false);
            var before = Snapshot(order);
            var now = clock.UtcNow;
            mutation(order, now);
            await PersistTransitionAsync(session, order, action, before, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    private async Task PersistTransitionAsync(
        IServiceDataSession session,
        WorkOrder order,
        string action,
        object before,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await session.WorkOrders.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
        await session.WorkOrders.AddStatusHistoryAsync(
            order.Id, order.StatusHistory.Last(), cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            session, nameof(WorkOrder), order.Id.ToString(), action,
            before, Snapshot(order), now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WorkOrder> RequireAsync(
        IServiceDataSession session,
        int id,
        bool withParts,
        CancellationToken cancellationToken)
    {
        var order = withParts
            ? await session.WorkOrders.GetWithPartsAsync(id, cancellationToken).ConfigureAwait(false)
            : await session.WorkOrders.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return order ?? throw new EntityNotFoundException(nameof(WorkOrder), id);
    }

    private static object Snapshot(WorkOrder order) => new
    {
        order.Status,
        order.AssignedTechnicianId,
        PlannedStart = order.PlannedWindow?.Start,
        PlannedEnd = order.PlannedWindow?.End,
        order.ActualStartAt,
        order.CompletedAt,
        order.ClosedAt,
        order.LaborMinutes,
    };

    private async Task InTransactionAsync(
        Func<IServiceDataSession, Task> action,
        CancellationToken cancellationToken)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action(session).ConfigureAwait(false);
            await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await session.UnitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
