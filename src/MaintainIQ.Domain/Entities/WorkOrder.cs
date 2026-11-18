using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Domain.Entities;

public abstract class WorkOrder : BaseEntity
{
    private readonly List<WorkOrderPart> _parts = [];
    private readonly List<WorkOrderStatusHistoryEntry> _statusHistory = [];
    private readonly List<WorkOrderComment> _comments = [];

    protected WorkOrder(
        string number,
        int equipmentId,
        Priority priority,
        string description,
        DateTimeOffset createdAt,
        DateRange? plannedWindow = null)
        : base(createdAt)
    {
        if (equipmentId <= 0)
        {
            throw new DomainValidationException("İş emri için geçerli bir ekipman seçilmelidir.");
        }

        Number = Guard.Required(number, "İş emri numarası", 50).ToUpperInvariant();
        EquipmentId = equipmentId;
        Priority = priority;
        Description = Guard.Required(description, "İş emri açıklaması", 2000);
        PlannedWindow = plannedWindow;
    }

    public string Number { get; private set; }
    public int EquipmentId { get; private set; }
    public WorkOrderStatus Status { get; protected set; } = WorkOrderStatus.Created;
    public Priority Priority { get; private set; }
    public string Description { get; private set; }
    public DateRange? PlannedWindow { get; private set; }
    public int? AssignedTechnicianId { get; protected set; }
    public DateTimeOffset? ActualStartAt { get; protected set; }
    public DateTimeOffset? CompletedAt { get; protected set; }
    public DateTimeOffset? ClosedAt { get; protected set; }
    public int LaborMinutes { get; protected set; }
    public IReadOnlyCollection<WorkOrderPart> Parts => _parts.AsReadOnly();
    public IReadOnlyCollection<WorkOrderStatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyCollection<WorkOrderComment> Comments => _comments.AsReadOnly();

    public void AddPart(WorkOrderPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        EnsureMutable();
        _parts.Add(part);
    }

    public void AddComment(WorkOrderComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        EnsureMutable();
        _comments.Add(comment);
    }

    public Money CalculateCost(Money hourlyLaborRate)
    {
        var partsCost = _parts.Aggregate(
            Money.Zero(hourlyLaborRate.Currency),
            (total, part) => total.Add(part.TotalCost));
        return partsCost.Add(hourlyLaborRate.Multiply(LaborMinutes / 60m));
    }

    protected void AddStatusHistory(WorkOrderStatusHistoryEntry entry) => _statusHistory.Add(entry);

    private void EnsureMutable()
    {
        if (Status is WorkOrderStatus.Closed or WorkOrderStatus.Cancelled)
        {
            throw new BusinessRuleException("BR-01", "Kapalı veya iptal edilmiş iş emri değiştirilemez.");
        }
    }
}

public sealed class CorrectiveWorkOrder : WorkOrder
{
    public CorrectiveWorkOrder(
        string number,
        int equipmentId,
        int failureId,
        Priority priority,
        string description,
        DateTimeOffset createdAt,
        DateRange? plannedWindow = null)
        : base(number, equipmentId, priority, description, createdAt, plannedWindow)
    {
        if (failureId <= 0)
        {
            throw new DomainValidationException("Düzeltici iş emri için geçerli bir arıza seçilmelidir.");
        }

        FailureId = failureId;
    }

    public int FailureId { get; private set; }
}

public sealed class PreventiveWorkOrder : WorkOrder
{
    public PreventiveWorkOrder(
        string number,
        int equipmentId,
        int maintenancePlanId,
        DateOnly dueDate,
        Priority priority,
        string description,
        DateTimeOffset createdAt,
        DateRange? plannedWindow = null)
        : base(number, equipmentId, priority, description, createdAt, plannedWindow)
    {
        if (maintenancePlanId <= 0)
        {
            throw new DomainValidationException("Önleyici iş emri için geçerli bir bakım planı seçilmelidir.");
        }

        MaintenancePlanId = maintenancePlanId;
        DueDate = dueDate;
    }

    public int MaintenancePlanId { get; private set; }
    public DateOnly DueDate { get; private set; }
}
