using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Domain.Entities;

public sealed class FailureCause : BaseEntity
{
    public FailureCause(string code, string name, DateTimeOffset createdAt)
        : base(createdAt)
    {
        Code = Guard.Required(code, "Arıza neden kodu", 40).ToUpperInvariant();
        Name = Guard.Required(name, "Arıza nedeni", 160);
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;
}

public sealed class Failure : BaseEntity
{
    public Failure(
        int equipmentId,
        int causeId,
        FailureSeverity severity,
        string description,
        int reportedByPersonId,
        DateTimeOffset reportedAt)
        : base(reportedAt)
    {
        if (equipmentId <= 0 || causeId <= 0 || reportedByPersonId <= 0)
        {
            throw new DomainValidationException("Arıza için ekipman, neden ve bildiren kişi zorunludur.");
        }

        EquipmentId = equipmentId;
        CauseId = causeId;
        Severity = severity;
        Description = Guard.Required(description, "Arıza açıklaması", 2000);
        ReportedByPersonId = reportedByPersonId;
        ReportedAt = reportedAt;
    }

    public int EquipmentId { get; private set; }

    public int CauseId { get; private set; }

    public FailureSeverity Severity { get; private set; }

    public FailureStatus Status { get; private set; } = FailureStatus.Open;

    public string Description { get; private set; }

    public int ReportedByPersonId { get; private set; }

    public DateTimeOffset ReportedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public void Resolve(DateTimeOffset resolvedAt)
    {
        if (resolvedAt < ReportedAt)
        {
            throw new DomainValidationException("Çözüm zamanı bildirim zamanından önce olamaz.");
        }

        Status = FailureStatus.Resolved;
        ResolvedAt = resolvedAt;
    }
}
