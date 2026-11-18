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

    public bool IsTerminal => Status is WorkOrderStatus.Closed or WorkOrderStatus.Cancelled;

    public void Assign(
        int technicianId,
        bool technicianIsActive,
        DateRange plannedWindow,
        bool hasScheduleConflict,
        bool conflictApproved,
        int changedByPersonId,
        DateTimeOffset changedAt)
    {
        EnsureActionContext(changedByPersonId, changedAt);
        EnsureStatus(WorkOrderStatus.Created, WorkOrderStatus.Assigned);
        if (technicianId <= 0 || !technicianIsActive)
        {
            throw new BusinessRuleException("BR-03", "İş emri yalnızca aktif bir teknisyene atanabilir.");
        }

        if (hasScheduleConflict && !conflictApproved)
        {
            throw new BusinessRuleException("BR-12", "Çakışan atama Supervisor veya Admin onayı gerektirir.");
        }

        var isReassignment = Status == WorkOrderStatus.Assigned;
        AssignedTechnicianId = technicianId;
        PlannedWindow = plannedWindow;
        TransitionTo(
            WorkOrderStatus.Assigned,
            changedByPersonId,
            changedAt,
            isReassignment ? "Teknisyen yeniden atandı." : null);
    }

    public void Unassign(int changedByPersonId, DateTimeOffset changedAt)
    {
        EnsureActionContext(changedByPersonId, changedAt);
        EnsureStatus(WorkOrderStatus.Assigned);
        AssignedTechnicianId = null;
        TransitionTo(WorkOrderStatus.Created, changedByPersonId, changedAt, "Teknisyen ataması kaldırıldı.");
    }

    public void Start(int startedByPersonId, bool hasElevatedPermission, DateTimeOffset startedAt)
    {
        EnsureActionContext(startedByPersonId, startedAt);
        EnsureStatus(WorkOrderStatus.Assigned);
        EnsureTechnicianPermission(startedByPersonId, hasElevatedPermission);
        ActualStartAt = startedAt;
        TransitionTo(WorkOrderStatus.InProgress, startedByPersonId, startedAt);
    }

    public void RequestParts(string reason, int changedByPersonId, DateTimeOffset changedAt)
    {
        EnsureActionContext(changedByPersonId, changedAt);
        EnsureStatus(WorkOrderStatus.InProgress);
        var normalizedReason = Guard.Required(reason, "Parça bekleme nedeni", 1000);
        TransitionTo(WorkOrderStatus.WaitingForParts, changedByPersonId, changedAt, normalizedReason);
    }

    public void Resume(int changedByPersonId, DateTimeOffset changedAt)
    {
        EnsureActionContext(changedByPersonId, changedAt);
        EnsureStatus(WorkOrderStatus.WaitingForParts);
        TransitionTo(WorkOrderStatus.InProgress, changedByPersonId, changedAt);
    }

    public void Complete(
        int laborMinutes,
        bool stockIsSufficient,
        int completedByPersonId,
        bool hasElevatedPermission,
        DateTimeOffset completedAt)
    {
        EnsureActionContext(completedByPersonId, completedAt);
        EnsureStatus(WorkOrderStatus.InProgress);
        EnsureTechnicianPermission(completedByPersonId, hasElevatedPermission);
        if (laborMinutes <= 0)
        {
            throw new BusinessRuleException("BR-04", "Tamamlama için işçilik süresi sıfırdan büyük olmalıdır.");
        }

        if (!stockIsSufficient)
        {
            throw new BusinessRuleException("BR-02", "Yetersiz stok nedeniyle iş emri tamamlanamaz.");
        }

        LaborMinutes = laborMinutes;
        CompletedAt = completedAt;
        TransitionTo(WorkOrderStatus.Completed, completedByPersonId, completedAt);
    }

    public void Reopen(string reason, UserRole actorRole, int changedByPersonId, DateTimeOffset changedAt)
    {
        EnsureActionContext(changedByPersonId, changedAt);
        EnsureElevatedRole(actorRole);
        EnsureStatus(WorkOrderStatus.Completed);
        var normalizedReason = Guard.Required(reason, "Yeniden açma gerekçesi", 1000);
        CompletedAt = null;
        TransitionTo(WorkOrderStatus.InProgress, changedByPersonId, changedAt, normalizedReason);
    }

    public void Close(UserRole actorRole, int changedByPersonId, DateTimeOffset closedAt)
    {
        EnsureActionContext(changedByPersonId, closedAt);
        EnsureElevatedRole(actorRole);
        EnsureStatus(WorkOrderStatus.Completed);
        ClosedAt = closedAt;
        TransitionTo(WorkOrderStatus.Closed, changedByPersonId, closedAt);
    }

    public void Cancel(string reason, UserRole actorRole, int changedByPersonId, DateTimeOffset changedAt)
    {
        EnsureActionContext(changedByPersonId, changedAt);
        EnsureElevatedRole(actorRole);
        EnsureStatus(
            WorkOrderStatus.Created,
            WorkOrderStatus.Assigned,
            WorkOrderStatus.InProgress,
            WorkOrderStatus.WaitingForParts);
        var normalizedReason = Guard.Required(reason, "İptal gerekçesi", 1000);
        TransitionTo(WorkOrderStatus.Cancelled, changedByPersonId, changedAt, normalizedReason);
    }

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

    private void EnsureStatus(params WorkOrderStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(Status))
        {
            throw new BusinessRuleException("WO-STATE", $"İş emri {Status} durumundayken bu işlem yapılamaz.");
        }
    }

    private void EnsureTechnicianPermission(int personId, bool hasElevatedPermission)
    {
        if (!hasElevatedPermission && AssignedTechnicianId != personId)
        {
            throw new BusinessRuleException("BR-19", "Teknisyen yalnızca kendisine atanmış iş emrini yürütebilir.");
        }
    }

    private static void EnsureElevatedRole(UserRole role)
    {
        if (role is not UserRole.Admin and not UserRole.Supervisor)
        {
            throw new BusinessRuleException("BR-06", "Bu işlem yalnızca Admin veya Supervisor tarafından yapılabilir.");
        }
    }

    private void EnsureActionContext(int personId, DateTimeOffset occurredAt)
    {
        if (personId <= 0)
        {
            throw new DomainValidationException("İşlemi yapan kişi zorunludur.");
        }

        if (occurredAt < CreatedAt)
        {
            throw new DomainValidationException("İşlem zamanı iş emri oluşturma zamanından önce olamaz.");
        }
    }

    private void TransitionTo(
        WorkOrderStatus targetStatus,
        int changedByPersonId,
        DateTimeOffset changedAt,
        string? reason = null)
    {
        var sourceStatus = Status;
        Status = targetStatus;
        AddStatusHistory(new WorkOrderStatusHistoryEntry(
            sourceStatus,
            targetStatus,
            changedByPersonId,
            changedAt,
            reason));
        MarkUpdated(changedAt);
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
