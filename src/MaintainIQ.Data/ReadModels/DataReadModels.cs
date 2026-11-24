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

/// <summary>KPI kapsamı: null kök tüm tesis; kök + alt ağaç veya yalnızca tek ekipman.</summary>
public sealed record AnalyticsScope(int? RootEquipmentId = null, bool IncludeDescendants = true)
{
    public static AnalyticsScope Plant { get; } = new();

    public static AnalyticsScope Subtree(int rootEquipmentId) => new(rootEquipmentId, true);

    public static AnalyticsScope Single(int equipmentId) => new(equipmentId, false);
}

public sealed class ScopeEquipmentDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public EquipmentStatus Status { get; init; }
    public bool IsCritical { get; init; }
    public DateTime? WarrantyEndDate { get; init; }
}

public sealed class FailureFactDto
{
    public int Id { get; init; }
    public int EquipmentId { get; init; }
    public string CauseCode { get; init; } = string.Empty;
    public string CauseName { get; init; } = string.Empty;
    public FailureSeverity Severity { get; init; }
    public FailureStatus Status { get; init; }
    public DateTimeOffset ReportedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public DateTimeOffset? WorkOrderCompletedAt { get; init; }
}

public sealed class WorkOrderFactDto
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public int EquipmentId { get; init; }
    public bool IsPreventive { get; init; }
    public int? FailureId { get; init; }
    public int? MaintenancePlanId { get; init; }
    public DateTime? DueDate { get; init; }
    public WorkOrderStatus Status { get; init; }
    public int? AssignedTechnicianId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ActualStartAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public int LaborMinutes { get; init; }
    public decimal PartsCost { get; init; }
}

public sealed class KpiFactSet
{
    public IReadOnlyList<ScopeEquipmentDto> Equipment { get; init; } = [];
    public IReadOnlyList<FailureFactDto> Failures { get; init; } = [];
    public IReadOnlyList<WorkOrderFactDto> WorkOrders { get; init; } = [];
}

public sealed class FailureParetoItemDto
{
    public string CauseCode { get; init; } = string.Empty;
    public string CauseName { get; init; } = string.Empty;
    public int FailureCount { get; init; }
    public decimal SharePercent { get; init; }
    public decimal CumulativePercent { get; init; }
}

public sealed class MonthlyFailureTrendDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public int FailureCount { get; init; }
    public decimal MovingAverage3 { get; init; }
}

public sealed class PartConsumptionDto
{
    public int Rank { get; init; }
    public int SparePartId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal TotalQuantity { get; init; }
    public decimal TotalCost { get; init; }
    public int WorkOrderCount { get; init; }
}

public sealed class TechnicianWorkloadDto
{
    public int TechnicianId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public int OpenWorkOrders { get; init; }
    public int CompletedInPeriod { get; init; }
    public decimal LaborHoursInPeriod { get; init; }
}

public sealed class EquipmentCostDto
{
    public int EquipmentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal PartsCost { get; init; }
    public int LaborMinutes { get; init; }
    public int ClosedWorkOrders { get; init; }
}

public sealed class PartReplacementFactDto
{
    public int SparePartId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal StockQuantity { get; init; }
    public decimal MinimumStock { get; init; }
    public int WorkOrderId { get; init; }
    public string WorkOrderNumber { get; init; } = string.Empty;
    public DateTimeOffset CompletedAt { get; init; }
}

public sealed class OverduePlanFactDto
{
    public int MaintenancePlanId { get; init; }
    public int EquipmentId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
    public string? OpenWorkOrderNumber { get; init; }
}

public sealed class AdvisorFactSet
{
    public IReadOnlyList<ScopeEquipmentDto> Equipment { get; init; } = [];
    public IReadOnlyList<FailureFactDto> Failures { get; init; } = [];
    public IReadOnlyList<WorkOrderFactDto> CompletedCorrectiveWorkOrders { get; init; } = [];
    public IReadOnlyList<PartReplacementFactDto> PartReplacements { get; init; } = [];
    public IReadOnlyList<OverduePlanFactDto> OverduePlans { get; init; } = [];
}
