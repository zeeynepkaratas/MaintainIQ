using MaintainIQ.Data.ReadModels;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Services.Equipment;

public sealed record CreateEquipmentCommand(string Code, string Name, string Location, int? ParentId = null);
public sealed record UpdateEquipmentCommand(int Id, string Name, string Location);

public interface IEquipmentService
{
    Task<int> CreateAsync(CreateEquipmentCommand command, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateEquipmentCommand command, CancellationToken cancellationToken = default);
    Task MoveAsync(int equipmentId, int? newParentId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int equipmentId, CancellationToken cancellationToken = default);
    Task ChangeStatusAsync(int equipmentId, EquipmentStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EquipmentTreeItemDto>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Domain.Entities.Equipment>> SearchAsync(string filter, CancellationToken cancellationToken = default);
}

public sealed class EquipmentService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IClock clock) : IEquipmentService
{
    private readonly AuditWriter _audit = new(currentUser);

    public async Task<int> CreateAsync(CreateEquipmentCommand command, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageEquipment);
        return await InTransactionAsync(async session =>
        {
            Domain.Entities.Equipment? parent = null;
            if (command.ParentId is int parentId)
            {
                parent = await RequireAsync(session, parentId, cancellationToken).ConfigureAwait(false);
            }

            var now = clock.UtcNow;
            var equipment = new Domain.Entities.Equipment(
                command.Code, command.Name, command.Location, now, Guid.NewGuid().ToString("N"));
            equipment.MoveUnder(parent);
            var id = await session.Equipment.AddAsync(equipment, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Domain.Entities.Equipment), id.ToString(), "Created", null,
                new { equipment.Code, equipment.Name, equipment.Location, equipment.ParentId, equipment.QrToken },
                now, cancellationToken).ConfigureAwait(false);
            return id;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(UpdateEquipmentCommand command, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageEquipment);
        return InTransactionAsync(async session =>
        {
            var equipment = await RequireAsync(session, command.Id, cancellationToken).ConfigureAwait(false);
            var oldValues = new { equipment.Name, equipment.Location };
            var now = clock.UtcNow;
            equipment.UpdateDetails(command.Name, command.Location, now);
            await session.Equipment.UpdateAsync(equipment, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Domain.Entities.Equipment), equipment.Id.ToString(), "Updated",
                oldValues, new { equipment.Name, equipment.Location }, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task MoveAsync(int equipmentId, int? newParentId, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageEquipment);
        return InTransactionAsync(async session =>
        {
            var equipment = await RequireAsync(session, equipmentId, cancellationToken).ConfigureAwait(false);
            Domain.Entities.Equipment? parent = null;
            if (newParentId is int parentId)
            {
                var descendants = await session.Equipment.GetDescendantsAsync(equipmentId, cancellationToken).ConfigureAwait(false);
                if (parentId == equipmentId || descendants.Any(item => item.Id == parentId))
                {
                    throw new Domain.Common.BusinessRuleException(
                        "BR-07", "Ekipman kendisine veya bir alt ekipmanına taşınamaz.");
                }

                parent = await RequireAsync(session, parentId, cancellationToken).ConfigureAwait(false);
            }

            var oldParentId = equipment.ParentId;
            var now = clock.UtcNow;
            equipment.MoveUnder(parent);
            equipment.MarkUpdated(now);
            await session.Equipment.UpdateAsync(equipment, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Domain.Entities.Equipment), equipment.Id.ToString(), "Moved",
                new { ParentId = oldParentId }, new { equipment.ParentId }, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task DeleteAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageEquipment);
        return InTransactionAsync(async session =>
        {
            var equipment = await RequireAsync(session, equipmentId, cancellationToken).ConfigureAwait(false);
            var descendants = await session.Equipment.GetDescendantsAsync(equipmentId, cancellationToken).ConfigureAwait(false);
            var hasOpenWork = await session.Equipment.ExistsOpenWorkAsync(equipmentId, cancellationToken).ConfigureAwait(false);
            if (descendants.Count > 0)
            {
                throw new Domain.Common.BusinessRuleException("BR-08", "Alt ekipmanı olan ekipman silinemez.");
            }

            var now = clock.UtcNow;
            equipment.SoftDelete(hasOpenWork);
            equipment.MarkUpdated(now);
            await session.Equipment.DeleteAsync(equipment, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Domain.Entities.Equipment), equipment.Id.ToString(), "SoftDeleted",
                new { IsDeleted = false }, new { equipment.IsDeleted }, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task ChangeStatusAsync(
        int equipmentId,
        EquipmentStatus status,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageEquipment);
        return InTransactionAsync(async session =>
        {
            var equipment = await RequireAsync(session, equipmentId, cancellationToken).ConfigureAwait(false);
            var oldStatus = equipment.Status;
            switch (status)
            {
                case EquipmentStatus.Operational: equipment.MarkOperational(); break;
                case EquipmentStatus.Faulty: equipment.MarkFaulty(); break;
                case EquipmentStatus.UnderMaintenance: equipment.MarkUnderMaintenance(); break;
                case EquipmentStatus.Retired: equipment.Retire(); break;
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }

            var now = clock.UtcNow;
            equipment.MarkUpdated(now);
            await session.Equipment.UpdateAsync(equipment, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(Domain.Entities.Equipment), equipment.Id.ToString(), "StatusChanged",
                new { Status = oldStatus }, new { equipment.Status }, now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<EquipmentTreeItemDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var result = await session.Equipment.GetTreeAsync(cancellationToken).ConfigureAwait(false);
        await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<Domain.Entities.Equipment>> SearchAsync(
        string filter,
        CancellationToken cancellationToken = default)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var result = await session.Equipment.SearchAsync(filter, cancellationToken).ConfigureAwait(false);
        await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<Domain.Entities.Equipment> RequireAsync(
        IServiceDataSession session,
        int id,
        CancellationToken cancellationToken) =>
        await session.Equipment.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException(nameof(Domain.Entities.Equipment), id);

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
