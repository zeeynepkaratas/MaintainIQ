namespace MaintainIQ.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Supervisor = 2,
    Technician = 3,
    Operator = 4,
}

public enum EquipmentStatus
{
    Operational = 1,
    Faulty = 2,
    UnderMaintenance = 3,
    Retired = 4,
}

public enum FailureSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public enum FailureStatus
{
    Open = 1,
    InReview = 2,
    Resolved = 3,
    Cancelled = 4,
}

public enum WorkOrderStatus
{
    Created = 1,
    Assigned = 2,
    InProgress = 3,
    WaitingForParts = 4,
    Completed = 5,
    Closed = 6,
    Cancelled = 7,
}

public enum Priority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4,
}

public enum StockMovementType
{
    Receipt = 1,
    Consumption = 2,
    Adjustment = 3,
    Return = 4,
}

public enum MaintenancePeriodUnit
{
    Day = 1,
    Week = 2,
    Month = 3,
    Year = 4,
}

public enum AttachmentOwnerType
{
    Equipment = 1,
    Failure = 2,
    WorkOrder = 3,
}
