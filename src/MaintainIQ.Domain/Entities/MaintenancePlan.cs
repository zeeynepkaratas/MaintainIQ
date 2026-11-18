using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Domain.Entities;

public sealed class MaintenancePlan : BaseEntity
{
    public MaintenancePlan(
        int equipmentId,
        string name,
        int periodValue,
        MaintenancePeriodUnit periodUnit,
        Priority priority,
        int estimatedDurationMinutes,
        DateOnly nextDueDate,
        DateTimeOffset createdAt)
        : base(createdAt)
    {
        if (equipmentId <= 0)
        {
            throw new DomainValidationException("Bakım planı için geçerli bir ekipman seçilmelidir.");
        }

        if (periodValue <= 0 || estimatedDurationMinutes <= 0)
        {
            throw new BusinessRuleException("BR-18", "Bakım periyodu ve tahmini süre pozitif olmalıdır.");
        }

        EquipmentId = equipmentId;
        Name = Guard.Required(name, "Bakım planı adı", 160);
        PeriodValue = periodValue;
        PeriodUnit = periodUnit;
        Priority = priority;
        EstimatedDurationMinutes = estimatedDurationMinutes;
        NextDueDate = nextDueDate;
    }

    public int EquipmentId { get; private set; }
    public string Name { get; private set; }
    public int PeriodValue { get; private set; }
    public MaintenancePeriodUnit PeriodUnit { get; private set; }
    public Priority Priority { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public DateOnly NextDueDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateOnly CalculateNextDue(DateOnly from) => PeriodUnit switch
    {
        MaintenancePeriodUnit.Day => from.AddDays(PeriodValue),
        MaintenancePeriodUnit.Week => from.AddDays(PeriodValue * 7),
        MaintenancePeriodUnit.Month => from.AddMonths(PeriodValue),
        MaintenancePeriodUnit.Year => from.AddYears(PeriodValue),
        _ => throw new DomainValidationException("Bilinmeyen bakım periyodu."),
    };

    public void AdvanceDueDate() => NextDueDate = CalculateNextDue(NextDueDate);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
