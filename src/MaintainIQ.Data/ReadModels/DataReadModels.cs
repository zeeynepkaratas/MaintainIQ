using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Data.ReadModels;

public sealed class EquipmentTreeItemDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public EquipmentStatus Status { get; init; }
    public int Level { get; init; }
    public string Path { get; init; } = string.Empty;
}

public sealed class WorkOrderListItemDto
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string EquipmentCode { get; init; } = string.Empty;
    public string EquipmentName { get; init; } = string.Empty;
    public WorkOrderStatus Status { get; init; }
    public Priority Priority { get; init; }
    public int? AssignedTechnicianId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PlannedStartAt { get; init; }
    public DateTimeOffset? PlannedEndAt { get; init; }
}

public sealed record WorkOrderFilter(
    WorkOrderStatus? Status = null,
    Priority? Priority = null,
    int? TechnicianId = null,
    int? EquipmentId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? SearchText = null,
    int Page = 1,
    int PageSize = 50,
    string SortBy = "CreatedAt",
    bool Descending = true);

public sealed class MaintenanceKpiDto
{
    public int OpenWorkOrders { get; init; }
    public int OverdueWorkOrders { get; init; }
    public decimal CompletionRate { get; init; }
    public decimal TotalMaintenanceCost { get; init; }
    public decimal MttrHours { get; init; }
    public decimal MtbfHours { get; init; }
}
