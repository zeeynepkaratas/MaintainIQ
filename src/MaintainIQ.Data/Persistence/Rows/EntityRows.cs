namespace MaintainIQ.Data.Persistence.Rows;

internal abstract class EntityRow
{
    public int Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

internal sealed class EquipmentRow : EntityRow
{
    public int? ParentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string? QrToken { get; init; }
    public byte Status { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsCritical { get; init; }
    public DateTime? WarrantyEndDate { get; init; }
}

internal sealed class SparePartRow : EntityRow
{
    public int? SupplierId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal StockQuantity { get; init; }
    public decimal MinimumStock { get; init; }
    public decimal UnitCost { get; init; }
    public string Currency { get; init; } = string.Empty;
}

internal sealed class FailureRow : EntityRow
{
    public int EquipmentId { get; init; }
    public int CauseId { get; init; }
    public byte Severity { get; init; }
    public byte Status { get; init; }
    public string Description { get; init; } = string.Empty;
    public int ReportedByPersonId { get; init; }
    public DateTimeOffset ReportedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
}

internal sealed class MaintenancePlanRow : EntityRow
{
    public int EquipmentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int PeriodValue { get; init; }
    public byte PeriodUnit { get; init; }
    public byte Priority { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public DateTime NextDueDate { get; init; }
    public bool IsActive { get; init; }
    public DateTime? AnchorDate { get; init; }
    public DateTimeOffset? LastDoneAt { get; init; }
}

internal sealed class TechnicianRow : EntityRow
{
    public string EmployeeNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Specialty { get; init; }
    public bool IsActive { get; init; }
}

internal sealed class UserAccountRow : EntityRow
{
    public int PersonId { get; init; }
    public string Username { get; init; } = string.Empty;
    public byte Role { get; init; }
    public string PasswordHash { get; init; } = string.Empty;
    public string PasswordSalt { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int FailedLoginCount { get; init; }
    public DateTimeOffset? LockedUntil { get; init; }
}

internal sealed class AttachmentRow : EntityRow
{
    public byte OwnerType { get; init; }
    public int OwnerId { get; init; }
    public string StoredFileName { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

internal sealed class StockMovementRow : EntityRow
{
    public int SparePartId { get; init; }
    public int? WorkOrderId { get; init; }
    public byte MovementType { get; init; }
    public decimal Quantity { get; init; }
    public decimal ResultingQuantity { get; init; }
    public decimal UnitCost { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

internal sealed class AuditLogRow : EntityRow
{
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public int PerformedByPersonId { get; init; }
    public DateTimeOffset PerformedAt { get; init; }
    public string? OldValuesJson { get; init; }
    public string? NewValuesJson { get; init; }
}

internal sealed class WorkOrderRow : EntityRow
{
    public byte WorkOrderType { get; init; }
    public string Number { get; init; } = string.Empty;
    public int EquipmentId { get; init; }
    public int? FailureId { get; init; }
    public int? MaintenancePlanId { get; init; }
    public DateTime? DueDate { get; init; }
    public byte Status { get; init; }
    public byte Priority { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset? PlannedStartAt { get; init; }
    public DateTimeOffset? PlannedEndAt { get; init; }
    public int? AssignedTechnicianId { get; init; }
    public DateTimeOffset? ActualStartAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public int LaborMinutes { get; init; }
}

internal sealed class WorkOrderPartRow : EntityRow
{
    public int WorkOrderId { get; init; }
    public int SparePartId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCostSnapshot { get; init; }
    public string Currency { get; init; } = string.Empty;
}
