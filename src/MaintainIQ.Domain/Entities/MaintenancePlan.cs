using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Domain.Entities;

/// <summary>
/// Takvim tabanlı bakım planı. Vade tarihleri <see cref="AnchorDate"/> + k × periyot formülüyle
/// hesaplanır; böylece 31 Ocak çapalı aylık plan 28 Şubat'tan sonra 31 Mart'a döner (ay sonu kayması olmaz).
/// </summary>
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

        EquipmentId = equipmentId;
        Name = Guard.Required(name, "Bakım planı adı", 160);
        ApplySchedule(periodValue, periodUnit, estimatedDurationMinutes);
        Priority = priority;
        NextDueDate = nextDueDate;
        AnchorDate = nextDueDate;
    }

    public int EquipmentId { get; private set; }
    public string Name { get; private set; }
    public int PeriodValue { get; private set; }
    public MaintenancePeriodUnit PeriodUnit { get; private set; }
    public Priority Priority { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public DateOnly NextDueDate { get; private set; }
    public DateOnly AnchorDate { get; private set; }
    public DateTimeOffset? LastDoneAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    internal static MaintenancePlan Rehydrate(
        int id, int equipmentId, string name, int periodValue, MaintenancePeriodUnit periodUnit,
        Priority priority, int estimatedDurationMinutes, DateOnly nextDueDate, bool isActive,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, byte[] rowVersion,
        DateOnly? anchorDate = null, DateTimeOffset? lastDoneAt = null)
    {
        var plan = new MaintenancePlan(
            equipmentId, name, periodValue, periodUnit, priority,
            estimatedDurationMinutes, nextDueDate, createdAt)
        {
            IsActive = isActive,
            AnchorDate = anchorDate ?? nextDueDate,
            LastDoneAt = lastDoneAt,
        };
        plan.RestorePersistenceState(id, createdAt, updatedAt, rowVersion);
        return plan;
    }

    /// <summary>Verilen tarihe bir periyot ekler (çapadan bağımsız basit hesap).</summary>
    public DateOnly CalculateNextDue(DateOnly from) => AddPeriods(from, 1);

    /// <summary>Takvimdeki, verilen tarihten kesinlikle sonraki ilk vade.</summary>
    public DateOnly NextOccurrenceAfter(DateOnly date)
    {
        if (date < AnchorDate)
        {
            return AnchorDate;
        }

        var k = EstimatePeriodsBetween(AnchorDate, date);
        var candidate = AddPeriods(AnchorDate, k);
        while (candidate <= date)
        {
            candidate = AddPeriods(AnchorDate, ++k);
        }

        while (k > 0 && AddPeriods(AnchorDate, k - 1) > date)
        {
            candidate = AddPeriods(AnchorDate, --k);
        }

        return candidate;
    }

    public void AdvanceDueDate() => NextDueDate = NextOccurrenceAfter(NextDueDate);

    public bool IsDue(DateOnly today) => IsActive && NextDueDate <= today;

    /// <summary>
    /// Mevcut vade için iş emri üretildiğinde çağrılır. Vade bugünün arkasında kalmışsa kaçırılan
    /// dönemler atlanır (tek iş emri üretilir) ve atlanan dönem sayısı döner.
    /// </summary>
    public int MarkOccurrenceGenerated(DateOnly today, DateTimeOffset changedAt)
    {
        if (!IsActive)
        {
            throw new BusinessRuleException("PM-INACTIVE", "Pasif bakım planı için iş emri üretilemez.");
        }

        if (NextDueDate > today)
        {
            throw new BusinessRuleException("PM-NOT-DUE", "Bakım planının vadesi henüz gelmedi.");
        }

        var generatedFor = NextDueDate;
        NextDueDate = NextOccurrenceAfter(today);
        MarkUpdated(changedAt);
        return CountOccurrencesBetween(generatedFor, today);
    }

    /// <summary>Planın önleyici iş emri tamamlandığında son yapılma zamanını ve sonraki vadeyi günceller.</summary>
    public void RecordCompletion(DateTimeOffset completedAt)
    {
        var completedOn = DateOnly.FromDateTime(completedAt.DateTime);
        LastDoneAt = completedAt;
        if (NextDueDate <= completedOn)
        {
            NextDueDate = NextOccurrenceAfter(completedOn);
        }

        MarkUpdated(completedAt);
    }

    public void Update(
        string name,
        int periodValue,
        MaintenancePeriodUnit periodUnit,
        Priority priority,
        int estimatedDurationMinutes,
        DateOnly nextDueDate,
        DateTimeOffset changedAt)
    {
        var scheduleChanged = nextDueDate != NextDueDate || periodValue != PeriodValue || periodUnit != PeriodUnit;
        Name = Guard.Required(name, "Bakım planı adı", 160);
        ApplySchedule(periodValue, periodUnit, estimatedDurationMinutes);
        Priority = priority;
        if (scheduleChanged)
        {
            AnchorDate = nextDueDate;
        }

        NextDueDate = nextDueDate;
        MarkUpdated(changedAt);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private void ApplySchedule(int periodValue, MaintenancePeriodUnit periodUnit, int estimatedDurationMinutes)
    {
        if (periodValue <= 0 || estimatedDurationMinutes <= 0)
        {
            throw new BusinessRuleException("BR-18", "Bakım periyodu ve tahmini süre pozitif olmalıdır.");
        }

        if (!Enum.IsDefined(periodUnit))
        {
            throw new DomainValidationException("Bilinmeyen bakım periyodu.");
        }

        PeriodValue = periodValue;
        PeriodUnit = periodUnit;
        EstimatedDurationMinutes = estimatedDurationMinutes;
    }

    private DateOnly AddPeriods(DateOnly from, int count) => PeriodUnit switch
    {
        MaintenancePeriodUnit.Day => from.AddDays(PeriodValue * count),
        MaintenancePeriodUnit.Week => from.AddDays(PeriodValue * 7 * count),
        MaintenancePeriodUnit.Month => from.AddMonths(PeriodValue * count),
        MaintenancePeriodUnit.Year => from.AddYears(PeriodValue * count),
        _ => throw new DomainValidationException("Bilinmeyen bakım periyodu."),
    };

    private int EstimatePeriodsBetween(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber;
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        var estimate = PeriodUnit switch
        {
            MaintenancePeriodUnit.Day => days / PeriodValue,
            MaintenancePeriodUnit.Week => days / (PeriodValue * 7),
            MaintenancePeriodUnit.Month => months / PeriodValue,
            MaintenancePeriodUnit.Year => (to.Year - from.Year) / PeriodValue,
            _ => 0,
        };
        return Math.Max(0, estimate);
    }

    private int CountOccurrencesBetween(DateOnly exclusiveFrom, DateOnly inclusiveTo)
    {
        var count = 0;
        for (var next = NextOccurrenceAfter(exclusiveFrom); next <= inclusiveTo; next = NextOccurrenceAfter(next))
        {
            count++;
        }

        return count;
    }
}
