using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Data.Persistence.Mappers;

internal static class EntityMappers
{
    public static Equipment ToDomain(this EquipmentRow row) => Equipment.Rehydrate(
        row.Id, row.ParentId, row.Code, row.Name, row.Location, row.QrToken,
        (EquipmentStatus)row.Status, row.IsDeleted, row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static SparePart ToDomain(this SparePartRow row) => SparePart.Rehydrate(
        row.Id, row.SupplierId, row.Code, row.Name, row.StockQuantity, row.MinimumStock,
        row.UnitCost, row.Currency, row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static Failure ToDomain(this FailureRow row) => Failure.Rehydrate(
        row.Id, row.EquipmentId, row.CauseId, (FailureSeverity)row.Severity,
        (FailureStatus)row.Status, row.Description, row.ReportedByPersonId, row.ReportedAt,
        row.ResolvedAt, row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static MaintenancePlan ToDomain(this MaintenancePlanRow row) => MaintenancePlan.Rehydrate(
        row.Id, row.EquipmentId, row.Name, row.PeriodValue, (MaintenancePeriodUnit)row.PeriodUnit,
        (Priority)row.Priority, row.EstimatedDurationMinutes, DateOnly.FromDateTime(row.NextDueDate),
        row.IsActive, row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static Technician ToDomain(this TechnicianRow row) => Technician.Rehydrate(
        row.Id, row.EmployeeNumber, row.FirstName, row.LastName, row.Specialty,
        row.IsActive, row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static UserAccount ToDomain(this UserAccountRow row) => UserAccount.Rehydrate(
        row.Id, row.PersonId, row.Username, (UserRole)row.Role, row.PasswordHash, row.PasswordSalt,
        row.IsActive, row.FailedLoginCount, row.LockedUntil, row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static Attachment ToDomain(this AttachmentRow row) => Attachment.Rehydrate(
        row.Id, (AttachmentOwnerType)row.OwnerType, row.OwnerId, row.StoredFileName,
        row.OriginalFileName, row.ContentType, row.SizeBytes, row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static StockMovement ToDomain(this StockMovementRow row) => StockMovement.Rehydrate(
        row.Id, row.SparePartId, row.WorkOrderId, (StockMovementType)row.MovementType,
        row.Quantity, row.ResultingQuantity, row.UnitCost, row.Currency, row.Reason,
        row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static AuditLog ToDomain(this AuditLogRow row) => AuditLog.Rehydrate(
        row.Id, row.EntityName, row.EntityId, row.Action, row.PerformedByPersonId,
        row.PerformedAt, row.OldValuesJson, row.NewValuesJson,
        row.CreatedAt, row.UpdatedAt, row.RowVersion);

    public static WorkOrder ToDomain(this WorkOrderRow row)
    {
        DateRange? window = row.PlannedStartAt.HasValue && row.PlannedEndAt.HasValue
            ? new DateRange(row.PlannedStartAt.Value, row.PlannedEndAt.Value)
            : null;

        return row.WorkOrderType switch
        {
            1 => CorrectiveWorkOrder.Rehydrate(
                row.Id, row.Number, row.EquipmentId, row.FailureId!.Value, (Priority)row.Priority,
                row.Description, window, (WorkOrderStatus)row.Status, row.AssignedTechnicianId,
                row.ActualStartAt, row.CompletedAt, row.ClosedAt, row.LaborMinutes,
                row.CreatedAt, row.UpdatedAt, row.RowVersion),
            2 => PreventiveWorkOrder.Rehydrate(
                row.Id, row.Number, row.EquipmentId, row.MaintenancePlanId!.Value,
                DateOnly.FromDateTime(row.DueDate!.Value), (Priority)row.Priority,
                row.Description, window, (WorkOrderStatus)row.Status, row.AssignedTechnicianId,
                row.ActualStartAt, row.CompletedAt, row.ClosedAt, row.LaborMinutes,
                row.CreatedAt, row.UpdatedAt, row.RowVersion),
            _ => throw new InvalidOperationException($"Bilinmeyen iş emri türü: {row.WorkOrderType}"),
        };
    }

    public static WorkOrderPart ToDomain(this WorkOrderPartRow row) => WorkOrderPart.Rehydrate(
        row.Id, row.SparePartId, row.Quantity, row.UnitCostSnapshot, row.Currency,
        row.CreatedAt, row.UpdatedAt, row.RowVersion);
}
