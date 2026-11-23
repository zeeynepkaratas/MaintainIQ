using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.ReadModels;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Data.Repositories;

public sealed class WorkOrderRepository(IUnitOfWork unitOfWork)
    : Repository<WorkOrder>(unitOfWork), IWorkOrderRepository
{
    private const string SelectByIdSql = """
        SELECT Id, WorkOrderType, Number, EquipmentId, FailureId, MaintenancePlanId, DueDate,
               Status, Priority, Description, PlannedStartAt, PlannedEndAt, AssignedTechnicianId,
               ActualStartAt, CompletedAt, ClosedAt, LaborMinutes, CreatedAt, UpdatedAt, RowVersion
        FROM dbo.WorkOrder WHERE Id = @Id;
        """;

    public override async Task<WorkOrder?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await UnitOfWork.Connection.QuerySingleOrDefaultAsync<WorkOrderRow>(
            Command(SelectByIdSql, new { Id = id }, cancellationToken)).ConfigureAwait(false);
        return row?.ToDomain();
    }

    public override async Task<IReadOnlyList<WorkOrder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, WorkOrderType, Number, EquipmentId, FailureId, MaintenancePlanId, DueDate,
                   Status, Priority, Description, PlannedStartAt, PlannedEndAt, AssignedTechnicianId,
                   ActualStartAt, CompletedAt, ClosedAt, LaborMinutes, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.WorkOrder ORDER BY CreatedAt DESC;
            """;
        var rows = await UnitOfWork.Connection.QueryAsync<WorkOrderRow>(
            Command(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(EntityMappers.ToDomain).ToArray();
    }

    public override Task<int> AddAsync(WorkOrder entity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT dbo.WorkOrder
                (WorkOrderType, Number, EquipmentId, FailureId, MaintenancePlanId, DueDate,
                 Status, Priority, Description, PlannedStartAt, PlannedEndAt, AssignedTechnicianId,
                 ActualStartAt, CompletedAt, ClosedAt, LaborMinutes, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@WorkOrderType, @Number, @EquipmentId, @FailureId, @MaintenancePlanId, @DueDate,
                 @Status, @Priority, @Description, @PlannedStartAt, @PlannedEndAt, @AssignedTechnicianId,
                 @ActualStartAt, @CompletedAt, @ClosedAt, @LaborMinutes, @CreatedAt, @UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(
            Command(sql, Parameters(entity), cancellationToken));
    }

    public override async Task UpdateAsync(WorkOrder entity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.WorkOrder
            SET Status = @Status, Priority = @Priority, Description = @Description,
                PlannedStartAt = @PlannedStartAt, PlannedEndAt = @PlannedEndAt,
                AssignedTechnicianId = @AssignedTechnicianId, ActualStartAt = @ActualStartAt,
                CompletedAt = @CompletedAt, ClosedAt = @ClosedAt,
                LaborMinutes = @LaborMinutes, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND RowVersion = @RowVersion;
            """;
        var affected = await UnitOfWork.Connection.ExecuteAsync(
            Command(sql, Parameters(entity), cancellationToken)).ConfigureAwait(false);
        EnsureUpdated(affected, nameof(WorkOrder));
    }

    public override async Task DeleteAsync(WorkOrder entity, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE dbo.WorkOrder WHERE Id = @Id AND RowVersion = @RowVersion;";
        var affected = await UnitOfWork.Connection.ExecuteAsync(
            Command(sql, new { entity.Id, entity.RowVersion }, cancellationToken)).ConfigureAwait(false);
        EnsureUpdated(affected, nameof(WorkOrder));
    }

    public async Task<IReadOnlyList<WorkOrderListItemDto>> SearchAsync(
        WorkOrderFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.Page <= 0 || filter.PageSize is <= 0 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Sayfa ve sayfa boyutu geçerli olmalıdır.");
        }

        const string sql = """
            SELECT w.Id, w.Number, e.Code AS EquipmentCode, e.Name AS EquipmentName,
                   w.Status, w.Priority, w.AssignedTechnicianId, w.CreatedAt,
                   w.PlannedStartAt, w.PlannedEndAt
            FROM dbo.WorkOrder w
            INNER JOIN dbo.Equipment e ON e.Id = w.EquipmentId
            WHERE (@Status IS NULL OR w.Status = @Status)
              AND (@Priority IS NULL OR w.Priority = @Priority)
              AND (@TechnicianId IS NULL OR w.AssignedTechnicianId = @TechnicianId)
              AND (@EquipmentId IS NULL OR w.EquipmentId = @EquipmentId)
              AND (@From IS NULL OR w.CreatedAt >= @From)
              AND (@To IS NULL OR w.CreatedAt < DATEADD(day, 1, @To))
              AND (@Search IS NULL OR w.Number LIKE @Search OR w.Description LIKE @Search
                   OR e.Code LIKE @Search OR e.Name LIKE @Search)
            ORDER BY
              CASE WHEN @SortBy = 'Number' AND @Descending = 0 THEN w.Number END ASC,
              CASE WHEN @SortBy = 'Number' AND @Descending = 1 THEN w.Number END DESC,
              CASE WHEN @SortBy = 'Priority' AND @Descending = 0 THEN w.Priority END ASC,
              CASE WHEN @SortBy = 'Priority' AND @Descending = 1 THEN w.Priority END DESC,
              CASE WHEN @SortBy = 'Status' AND @Descending = 0 THEN w.Status END ASC,
              CASE WHEN @SortBy = 'Status' AND @Descending = 1 THEN w.Status END DESC,
              CASE WHEN @SortBy = 'CreatedAt' AND @Descending = 0 THEN w.CreatedAt END ASC,
              CASE WHEN @SortBy = 'CreatedAt' AND @Descending = 1 THEN w.CreatedAt END DESC,
              w.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;
        var allowedSort = filter.SortBy is "Number" or "Priority" or "Status" or "CreatedAt"
            ? filter.SortBy
            : "CreatedAt";
        var items = await UnitOfWork.Connection.QueryAsync<WorkOrderListItemDto>(Command(sql, new
        {
            Status = (byte?)filter.Status,
            Priority = (byte?)filter.Priority,
            filter.TechnicianId,
            filter.EquipmentId,
            From = filter.From?.ToDateTime(TimeOnly.MinValue),
            To = filter.To?.ToDateTime(TimeOnly.MinValue),
            Search = string.IsNullOrWhiteSpace(filter.SearchText) ? null : $"%{filter.SearchText.Trim()}%",
            SortBy = allowedSort,
            filter.Descending,
            Offset = (filter.Page - 1) * filter.PageSize,
            filter.PageSize,
        }, cancellationToken)).ConfigureAwait(false);
        return items.ToArray();
    }

    public async Task<WorkOrder?> GetWithPartsAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return null;
        }

        const string sql = """
            SELECT Id, WorkOrderId, SparePartId, Quantity, UnitCostSnapshot, Currency,
                   CreatedAt, UpdatedAt, RowVersion
            FROM dbo.WorkOrderPart WHERE WorkOrderId = @Id ORDER BY Id;
            """;
        var rows = await UnitOfWork.Connection.QueryAsync<WorkOrderPartRow>(
            Command(sql, new { Id = id }, cancellationToken)).ConfigureAwait(false);
        foreach (var row in rows)
        {
            order.AddRehydratedPart(row.ToDomain());
        }

        return order;
    }

    public async Task<IReadOnlyList<WorkOrder>> GetOpenForEquipmentAsync(
        int equipmentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, WorkOrderType, Number, EquipmentId, FailureId, MaintenancePlanId, DueDate,
                   Status, Priority, Description, PlannedStartAt, PlannedEndAt, AssignedTechnicianId,
                   ActualStartAt, CompletedAt, ClosedAt, LaborMinutes, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.WorkOrder
            WHERE EquipmentId = @EquipmentId AND Status NOT IN (6, 7)
            ORDER BY CreatedAt DESC;
            """;
        var rows = await UnitOfWork.Connection.QueryAsync<WorkOrderRow>(
            Command(sql, new { EquipmentId = equipmentId }, cancellationToken)).ConfigureAwait(false);
        return rows.Select(EntityMappers.ToDomain).ToArray();
    }

    public Task AddPartAsync(
        int workOrderId,
        WorkOrderPart part,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT dbo.WorkOrderPart
                (WorkOrderId, SparePartId, Quantity, UnitCostSnapshot, Currency, CreatedAt, UpdatedAt)
            VALUES
                (@WorkOrderId, @SparePartId, @Quantity, @UnitCostSnapshot, @Currency, @CreatedAt, @UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteAsync(Command(sql, new
        {
            WorkOrderId = workOrderId,
            part.SparePartId,
            part.Quantity,
            UnitCostSnapshot = part.UnitCostSnapshot.Amount,
            part.UnitCostSnapshot.Currency,
            part.CreatedAt,
            part.UpdatedAt,
        }, cancellationToken));
    }

    public Task AddStatusHistoryAsync(
        int workOrderId,
        WorkOrderStatusHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT dbo.WorkOrderStatusHistory
                (WorkOrderId, FromStatus, ToStatus, ChangedByPersonId, ChangedAt, Reason, CreatedAt, UpdatedAt)
            VALUES
                (@WorkOrderId, @FromStatus, @ToStatus, @ChangedByPersonId, @ChangedAt, @Reason, @CreatedAt, @UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteAsync(Command(sql, new
        {
            WorkOrderId = workOrderId,
            FromStatus = (byte)entry.FromStatus,
            ToStatus = (byte)entry.ToStatus,
            entry.ChangedByPersonId,
            entry.ChangedAt,
            entry.Reason,
            entry.CreatedAt,
            entry.UpdatedAt,
        }, cancellationToken));
    }

    public Task<bool> HasScheduleConflictAsync(
        int technicianId,
        DateRange plannedWindow,
        int? excludedWorkOrderId = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CAST(CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.WorkOrder
                WHERE AssignedTechnicianId = @TechnicianId
                  AND (@ExcludedWorkOrderId IS NULL OR Id <> @ExcludedWorkOrderId)
                  AND Status IN (2, 3, 4)
                  AND PlannedStartAt IS NOT NULL
                  AND PlannedEndAt IS NOT NULL
                  AND PlannedStartAt < @PlannedEndAt
                  AND @PlannedStartAt < PlannedEndAt
            ) THEN 1 ELSE 0 END AS bit);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<bool>(Command(sql, new
        {
            TechnicianId = technicianId,
            ExcludedWorkOrderId = excludedWorkOrderId,
            PlannedStartAt = plannedWindow.Start,
            PlannedEndAt = plannedWindow.End,
        }, cancellationToken));
    }

    private static object Parameters(WorkOrder entity)
    {
        var corrective = entity as CorrectiveWorkOrder;
        var preventive = entity as PreventiveWorkOrder;
        return new
        {
            entity.Id,
            WorkOrderType = corrective is not null ? (byte)1 : (byte)2,
            entity.Number,
            entity.EquipmentId,
            FailureId = corrective?.FailureId,
            MaintenancePlanId = preventive?.MaintenancePlanId,
            DueDate = preventive?.DueDate.ToDateTime(TimeOnly.MinValue),
            Status = (byte)entity.Status,
            Priority = (byte)entity.Priority,
            entity.Description,
            PlannedStartAt = entity.PlannedWindow?.Start,
            PlannedEndAt = entity.PlannedWindow?.End,
            entity.AssignedTechnicianId,
            entity.ActualStartAt,
            entity.CompletedAt,
            entity.ClosedAt,
            entity.LaborMinutes,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.RowVersion,
        };
    }
}
