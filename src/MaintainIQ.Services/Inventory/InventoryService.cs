using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Services.Inventory;

public interface IInventoryService
{
    Task ReceiveAsync(int sparePartId, decimal quantity, Money unitCost, CancellationToken cancellationToken = default);
    Task AdjustAsync(int sparePartId, decimal newQuantity, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparePart>> GetBelowMinimumAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovement>> GetMovementsAsync(CancellationToken cancellationToken = default);
}

public sealed class InventoryService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IClock clock) : IInventoryService
{
    private readonly AuditWriter _audit = new(currentUser);

    public Task ReceiveAsync(
        int sparePartId,
        decimal quantity,
        Money unitCost,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageInventory);
        return ChangeStockAsync(sparePartId, "Received", async (session, part, now) =>
        {
            var oldQuantity = part.StockQuantity;
            part.Receive(quantity, unitCost);
            part.MarkUpdated(now);
            await session.SpareParts.UpdateAsync(part, cancellationToken).ConfigureAwait(false);
            await session.StockMovements.AddAsync(new StockMovement(
                part.Id, StockMovementType.Receipt, quantity, part.StockQuantity,
                unitCost, now, "Stok girişi"), cancellationToken).ConfigureAwait(false);
            return new { OldQuantity = oldQuantity, part.StockQuantity, UnitCost = unitCost.Amount };
        }, cancellationToken);
    }

    public Task AdjustAsync(
        int sparePartId,
        decimal newQuantity,
        string reason,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageInventory);
        return ChangeStockAsync(sparePartId, "Adjusted", async (session, part, now) =>
        {
            var oldQuantity = part.StockQuantity;
            part.Adjust(newQuantity, reason);
            part.MarkUpdated(now);
            await session.SpareParts.UpdateAsync(part, cancellationToken).ConfigureAwait(false);
            var delta = Math.Abs(newQuantity - oldQuantity);
            if (delta > 0)
            {
                await session.StockMovements.AddAsync(new StockMovement(
                    part.Id, StockMovementType.Adjustment, delta, part.StockQuantity,
                    part.UnitCost, now, reason), cancellationToken).ConfigureAwait(false);
            }

            return new { OldQuantity = oldQuantity, part.StockQuantity, Reason = reason };
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SparePart>> GetBelowMinimumAsync(CancellationToken cancellationToken = default)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var result = await session.SpareParts.GetBelowMinimumAsync(cancellationToken).ConfigureAwait(false);
        await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<StockMovement>> GetMovementsAsync(CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageInventory);
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var result = await session.StockMovements.GetAllAsync(cancellationToken).ConfigureAwait(false);
        await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task ChangeStockAsync(
        int sparePartId,
        string action,
        Func<IServiceDataSession, SparePart, DateTimeOffset, Task<object>> change,
        CancellationToken cancellationToken)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var part = await session.SpareParts.GetForUpdateAsync(sparePartId, cancellationToken).ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(SparePart), sparePartId);
            var now = clock.UtcNow;
            var oldValues = new { part.StockQuantity, UnitCost = part.UnitCost.Amount };
            var newValues = await change(session, part, now).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(SparePart), part.Id.ToString(), action,
                oldValues, newValues, now, cancellationToken).ConfigureAwait(false);
            await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await session.UnitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
