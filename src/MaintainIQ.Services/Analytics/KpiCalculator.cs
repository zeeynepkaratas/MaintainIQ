using MaintainIQ.Data.ReadModels;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Services.Analytics;

public sealed class KpiOptions
{
    /// <summary>İşçilik maliyeti için saatlik ücret (Ek E: ekipman maliyeti).</summary>
    public decimal HourlyLaborRate { get; init; } = 500m;

    public string Currency { get; init; } = "TRY";

    /// <summary>PM uyumunda vadeden sonra kabul edilen gün sayısı.</summary>
    public int PmToleranceDays { get; init; } = 3;
}

public sealed record BacklogAging(int UpTo7Days, int From8To30Days, int Over30Days)
{
    public int Total => UpTo7Days + From8To30Days + Over30Days;
}

public sealed record KpiReport(
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset ObservedUntil,
    int EquipmentCount,
    int FailureCount,
    decimal DowntimeHours,
    decimal? MttrHours,
    int CompletedCorrectiveCount,
    decimal? MtbfHours,
    decimal? AvailabilityPercent,
    decimal? PmCompliancePercent,
    int PmDueCount,
    int PmOnTimeCount,
    BacklogAging Backlog,
    decimal? AverageClosureHours,
    decimal? PlannedMaintenancePercent,
    decimal TotalCost,
    string Currency)
{
    /// <summary>Dönemde arıza yoksa MTBF "arızasız" gösterilir (Ek E).</summary>
    public bool IsFailureFree => FailureCount == 0;
}

/// <summary>
/// Ek E KPI formüllerinin saf hesaplayıcısı. SQL yalnızca kapsamdaki ham olguları getirir; formüller
/// burada tek yerde, elle hesaplanmış örneklerle test edilerek kilitlenir. Ayrıntılar: docs/kpi.md.
/// </summary>
public static class KpiCalculator
{
    private static readonly WorkOrderStatus[] BacklogStatuses =
    [
        WorkOrderStatus.Created,
        WorkOrderStatus.Assigned,
        WorkOrderStatus.InProgress,
        WorkOrderStatus.WaitingForParts,
    ];

    public static KpiReport Calculate(
        KpiFactSet facts,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now,
        KpiOptions options)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(options);
        if (to <= from)
        {
            throw new ArgumentException("Dönem bitişi başlangıçtan sonra olmalıdır.", nameof(to));
        }

        var observedUntil = now < to ? (now > from ? now : from) : to;
        var observedHours = (decimal)(observedUntil - from).TotalHours;
        var equipmentCount = facts.Equipment.Count;
        var totalOperatingHours = observedHours * equipmentCount;

        var failureCount = facts.Failures.Count(f => f.ReportedAt >= from && f.ReportedAt < observedUntil);
        var downtimeHours = CalculateDowntimeHours(facts.Failures, from, observedUntil, now);

        var repairs = facts.WorkOrders
            .Where(w => !w.IsPreventive && w.ActualStartAt is not null && w.CompletedAt is { } done
                && done >= from && done < observedUntil)
            .Select(w => (decimal)(w.CompletedAt!.Value - w.ActualStartAt!.Value).TotalHours)
            .ToArray();
        decimal? mttr = repairs.Length == 0 ? null : Round(repairs.Sum() / repairs.Length);

        var uptime = Math.Max(0m, totalOperatingHours - downtimeHours);
        decimal? mtbf = failureCount == 0 ? null : Round(uptime / failureCount);
        decimal? availability = totalOperatingHours <= 0 ? null : Round(uptime / totalOperatingHours * 100m);

        var (pmDue, pmOnTime) = CalculatePmCompliance(facts.WorkOrders, from, observedUntil, now, options.PmToleranceDays);
        decimal? pmCompliance = pmDue == 0 ? null : Round(pmOnTime * 100m / pmDue);

        var backlog = CalculateBacklog(facts.WorkOrders, now);

        var closures = facts.WorkOrders
            .Where(w => w.Status == WorkOrderStatus.Closed && w.ClosedAt is { } closed
                && closed >= from && closed < observedUntil)
            .Select(w => (decimal)(w.ClosedAt!.Value - w.CreatedAt).TotalHours)
            .ToArray();
        decimal? averageClosure = closures.Length == 0 ? null : Round(closures.Sum() / closures.Length);

        var createdInPeriod = facts.WorkOrders.Where(w => w.CreatedAt >= from && w.CreatedAt < observedUntil).ToArray();
        decimal? plannedRatio = createdInPeriod.Length == 0
            ? null
            : Round(createdInPeriod.Count(w => w.IsPreventive) * 100m / createdInPeriod.Length);

        var totalCost = facts.WorkOrders
            .Where(w => w.Status == WorkOrderStatus.Closed && w.ClosedAt is { } closed
                && closed >= from && closed < observedUntil)
            .Sum(w => WorkOrderCost(w.PartsCost, w.LaborMinutes, options.HourlyLaborRate));

        return new KpiReport(
            from, to, observedUntil, equipmentCount, failureCount, Round(downtimeHours), mttr, repairs.Length,
            mtbf, availability, pmCompliance, pmDue, pmOnTime, backlog, averageClosure, plannedRatio,
            Round(totalCost), options.Currency);
    }

    public static decimal WorkOrderCost(decimal partsCost, int laborMinutes, decimal hourlyLaborRate) =>
        Round(partsCost + (laborMinutes / 60m * hourlyLaborRate));

    /// <summary>
    /// Duruş: arıza bildirimi → düzeltici iş emri bitişi (yoksa arıza çözümü, o da yoksa "şimdi").
    /// Aynı ekipmandaki çakışan duruşlar birleştirilir; böylece aynı saat iki kez sayılmaz.
    /// </summary>
    public static decimal CalculateDowntimeHours(
        IEnumerable<FailureFactDto> failures,
        DateTimeOffset from,
        DateTimeOffset until,
        DateTimeOffset now)
    {
        var total = 0m;
        foreach (var equipmentFailures in failures.GroupBy(f => f.EquipmentId))
        {
            var intervals = equipmentFailures
                .Select(f =>
                {
                    var start = f.ReportedAt < from ? from : f.ReportedAt;
                    var end = f.WorkOrderCompletedAt ?? f.ResolvedAt ?? now;
                    if (end > until)
                    {
                        end = until;
                    }

                    return (Start: start, End: end);
                })
                .Where(interval => interval.End > interval.Start)
                .OrderBy(interval => interval.Start)
                .ToList();

            DateTimeOffset? currentStart = null;
            DateTimeOffset currentEnd = default;
            foreach (var (start, end) in intervals)
            {
                if (currentStart is null)
                {
                    (currentStart, currentEnd) = (start, end);
                }
                else if (start <= currentEnd)
                {
                    currentEnd = end > currentEnd ? end : currentEnd;
                }
                else
                {
                    total += (decimal)(currentEnd - currentStart.Value).TotalHours;
                    (currentStart, currentEnd) = (start, end);
                }
            }

            if (currentStart is not null)
            {
                total += (decimal)(currentEnd - currentStart.Value).TotalHours;
            }
        }

        return total;
    }

    private static (int Due, int OnTime) CalculatePmCompliance(
        IEnumerable<WorkOrderFactDto> workOrders,
        DateTimeOffset from,
        DateTimeOffset until,
        DateTimeOffset now,
        int toleranceDays)
    {
        var fromDate = DateOnly.FromDateTime(from.DateTime);
        var untilDate = DateOnly.FromDateTime(until.DateTime);
        var today = DateOnly.FromDateTime(now.DateTime);
        var due = 0;
        var onTime = 0;
        foreach (var order in workOrders.Where(w => w.IsPreventive && w.DueDate is not null
            && w.Status != WorkOrderStatus.Cancelled))
        {
            var dueDate = DateOnly.FromDateTime(order.DueDate!.Value);
            if (dueDate < fromDate || dueDate > untilDate)
            {
                continue;
            }

            var deadline = dueDate.AddDays(toleranceDays);
            var completedOn = order.CompletedAt is { } completed ? DateOnly.FromDateTime(completed.DateTime) : (DateOnly?)null;
            var judgeable = completedOn is not null || deadline < today;
            if (!judgeable)
            {
                // Tolerans penceresi henüz kapanmadı; uyumlu/uyumsuz denemez.
                continue;
            }

            due++;
            if (completedOn is { } doneOn && doneOn <= deadline)
            {
                onTime++;
            }
        }

        return (due, onTime);
    }

    private static BacklogAging CalculateBacklog(IEnumerable<WorkOrderFactDto> workOrders, DateTimeOffset now)
    {
        var upTo7 = 0;
        var upTo30 = 0;
        var older = 0;
        foreach (var order in workOrders.Where(w => BacklogStatuses.Contains(w.Status)))
        {
            var ageDays = (now - order.CreatedAt).TotalDays;
            if (ageDays <= 7)
            {
                upTo7++;
            }
            else if (ageDays <= 30)
            {
                upTo30++;
            }
            else
            {
                older++;
            }
        }

        return new BacklogAging(upTo7, upTo30, older);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
