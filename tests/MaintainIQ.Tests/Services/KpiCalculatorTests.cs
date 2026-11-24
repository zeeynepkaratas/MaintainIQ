using MaintainIQ.Data.ReadModels;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Analytics;

namespace MaintainIQ.Tests.Services;

/// <summary>
/// Elle hesaplanmış veri seti (docs/kpi.md "Örnek hesap" bölümüyle aynı):
/// Dönem 01.11.2026-01.12.2026 = 720 saat, 2 ekipman → 1440 ekipman-saat.
/// </summary>
public sealed class KpiCalculatorTests
{
    private static readonly DateTimeOffset From = At(11, 1, 0);
    private static readonly DateTimeOffset To = At(12, 1, 0);
    private static readonly DateTimeOffset Now = At(12, 5, 0);
    private static readonly KpiOptions Options = new() { HourlyLaborRate = 500m, PmToleranceDays = 3 };

    [Fact]
    public void Calculate_MatchesHandCalculatedExample()
    {
        var report = KpiCalculator.Calculate(SampleFacts(), From, To, Now, Options);

        Assert.Equal(2, report.EquipmentCount);
        Assert.Equal(To, report.ObservedUntil);

        // Duruş: F1 4s + F2 6s + F3 24s (iş emri yok → çözüm zamanı) + F4 dönem içi 2s = 36s.
        Assert.Equal(36m, report.DowntimeHours);
        // Dönemde bildirilen arıza: F1, F2, F3 (F4 dönemden önce bildirildi).
        Assert.Equal(3, report.FailureCount);
        Assert.False(report.IsFailureFree);
        // MTBF = (1440 - 36) / 3 = 468 saat.
        Assert.Equal(468m, report.MtbfHours);
        // Availability = 1404 / 1440 = %97,50.
        Assert.Equal(97.5m, report.AvailabilityPercent);
        // MTTR = (3 + 5 + 2) / 3 = 3,33 saat.
        Assert.Equal(3, report.CompletedCorrectiveCount);
        Assert.Equal(3.33m, report.MttrHours);
        // PM: P1 zamanında, P2 geç, P3 tamamlanmadı ve tolerans geçti, P4 iptal → 1 / 3.
        Assert.Equal(3, report.PmDueCount);
        Assert.Equal(1, report.PmOnTimeCount);
        Assert.Equal(33.33m, report.PmCompliancePercent);
        // Backlog: W8 (≤7 gün), P3 (8-30 gün), W6 (>30 gün).
        Assert.Equal(new BacklogAging(1, 1, 1), report.Backlog);
        // Ortalama kapanış = (24 + 48) / 2 = 36 saat.
        Assert.Equal(36m, report.AverageClosureHours);
        // Planlı oran = 4 önleyici / 7 dönem içi iş emri = %57,14.
        Assert.Equal(57.14m, report.PlannedMaintenancePercent);
        // Maliyet (yalnız kapalı): W1 100 + 1s×500 = 600; W2 250 + 1,5s×500 = 1000 → 1600.
        Assert.Equal(1600m, report.TotalCost);
        Assert.Equal("TRY", report.Currency);
    }

    [Fact]
    public void Calculate_WithNoFailuresOrWork_GuardsAgainstDivisionByZero()
    {
        var facts = new KpiFactSet { Equipment = [Equipment(1)] };

        var report = KpiCalculator.Calculate(facts, From, To, Now, Options);

        Assert.True(report.IsFailureFree);
        Assert.Null(report.MtbfHours);
        Assert.Null(report.MttrHours);
        Assert.Null(report.PmCompliancePercent);
        Assert.Null(report.AverageClosureHours);
        Assert.Null(report.PlannedMaintenancePercent);
        Assert.Equal(100m, report.AvailabilityPercent);
        Assert.Equal(0, report.Backlog.Total);
        Assert.Equal(0m, report.TotalCost);
    }

    [Fact]
    public void Calculate_WithEmptyScope_ReturnsNoAvailability()
    {
        var report = KpiCalculator.Calculate(new KpiFactSet(), From, To, Now, Options);

        Assert.Equal(0, report.EquipmentCount);
        Assert.Null(report.AvailabilityPercent);
        Assert.Null(report.MtbfHours);
    }

    [Fact]
    public void Calculate_ForRunningPeriod_ObservesOnlyUntilNow()
    {
        var now = At(11, 11, 0);
        var facts = new KpiFactSet
        {
            Equipment = [Equipment(1)],
            Failures = [Failure(1, 1, At(11, 10, 0), completedAt: null)],
        };

        var report = KpiCalculator.Calculate(facts, From, To, now, Options);

        Assert.Equal(now, report.ObservedUntil);
        // Açık arıza "şimdi"ye kadar sayılır: 24 saat. MTBF = (240 - 24) / 1 = 216.
        Assert.Equal(24m, report.DowntimeHours);
        Assert.Equal(216m, report.MtbfHours);
        Assert.Equal(90m, report.AvailabilityPercent);
    }

    [Fact]
    public void CalculateDowntime_MergesOverlappingFailuresOnSameEquipment()
    {
        var failures = new[]
        {
            Failure(1, 1, At(11, 2, 8), completedAt: At(11, 2, 12)),
            Failure(2, 1, At(11, 2, 10), completedAt: At(11, 2, 14)),
            Failure(3, 2, At(11, 2, 10), completedAt: At(11, 2, 11)),
        };

        var downtime = KpiCalculator.CalculateDowntimeHours(failures, From, To, Now);

        // Ekipman 1: 08-14 birleşik = 6s; ekipman 2: 1s.
        Assert.Equal(7m, downtime);
    }

    [Fact]
    public void Calculate_RejectsInvalidPeriod()
    {
        Assert.Throws<ArgumentException>(() => KpiCalculator.Calculate(new KpiFactSet(), To, From, Now, Options));
    }

    [Fact]
    public void WorkOrderCost_AddsLaborAtHourlyRate()
    {
        Assert.Equal(1000m, KpiCalculator.WorkOrderCost(250m, 90, 500m));
    }

    internal static KpiFactSet SampleFacts() => new()
    {
        Equipment = [Equipment(1), Equipment(2)],
        Failures =
        [
            Failure(1, 1, At(11, 2, 8), completedAt: At(11, 2, 12)),
            Failure(2, 1, At(11, 10, 10), completedAt: At(11, 10, 16)),
            Failure(3, 2, At(11, 20, 0), completedAt: null, resolvedAt: At(11, 21, 0)),
            Failure(4, 2, At(10, 31, 22), completedAt: At(11, 1, 2)),
        ],
        WorkOrders =
        [
            Corrective(1, 1, 1, WorkOrderStatus.Closed, created: At(11, 2, 8, 30), start: At(11, 2, 9), done: At(11, 2, 12),
                closed: At(11, 3, 8, 30), labor: 60, parts: 100m),
            Corrective(2, 1, 2, WorkOrderStatus.Closed, created: At(11, 10, 10, 30), start: At(11, 10, 11), done: At(11, 10, 16),
                closed: At(11, 12, 10, 30), labor: 90, parts: 250m),
            Corrective(4, 2, 4, WorkOrderStatus.Completed, created: At(10, 31, 23), start: At(11, 1, 0), done: At(11, 1, 2),
                closed: null, labor: 120, parts: 0m),
            Corrective(6, 2, 6, WorkOrderStatus.InProgress, created: At(10, 1, 0), start: At(10, 2, 0), done: null,
                closed: null, labor: 0, parts: 0m),
            Corrective(8, 1, 8, WorkOrderStatus.Assigned, created: At(11, 29, 23), start: null, done: null,
                closed: null, labor: 0, parts: 0m),
            Preventive(11, 1, new DateTime(2026, 11, 5), WorkOrderStatus.Completed, At(11, 4, 0), At(11, 7, 10)),
            Preventive(12, 2, new DateTime(2026, 11, 15), WorkOrderStatus.Completed, At(11, 14, 0), At(11, 20, 10)),
            Preventive(13, 1, new DateTime(2026, 11, 28), WorkOrderStatus.Created, At(11, 25, 0), null),
            Preventive(14, 2, new DateTime(2026, 11, 30), WorkOrderStatus.Cancelled, At(11, 29, 0), null),
        ],
    };

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    private static ScopeEquipmentDto Equipment(int id) => new() { Id = id, Code = $"EQ-{id}", Name = $"Ekipman {id}" };

    private static FailureFactDto Failure(
        int id, int equipmentId, DateTimeOffset reportedAt, DateTimeOffset? completedAt, DateTimeOffset? resolvedAt = null) => new()
    {
        Id = id,
        EquipmentId = equipmentId,
        CauseCode = "MECHANICAL",
        CauseName = "Mekanik arıza",
        Severity = FailureSeverity.High,
        Status = completedAt is null && resolvedAt is null ? FailureStatus.Open : FailureStatus.Resolved,
        ReportedAt = reportedAt,
        ResolvedAt = resolvedAt ?? completedAt,
        WorkOrderCompletedAt = completedAt,
    };

    private static WorkOrderFactDto Corrective(
        int id, int equipmentId, int failureId, WorkOrderStatus status, DateTimeOffset created,
        DateTimeOffset? start, DateTimeOffset? done, DateTimeOffset? closed, int labor, decimal parts) => new()
    {
        Id = id,
        Number = $"WO-{id}",
        EquipmentId = equipmentId,
        FailureId = failureId,
        Status = status,
        CreatedAt = created,
        ActualStartAt = start,
        CompletedAt = done,
        ClosedAt = closed,
        LaborMinutes = labor,
        PartsCost = parts,
    };

    private static WorkOrderFactDto Preventive(
        int id, int equipmentId, DateTime dueDate, WorkOrderStatus status, DateTimeOffset created, DateTimeOffset? done) => new()
    {
        Id = id,
        Number = $"PM-{id}",
        EquipmentId = equipmentId,
        IsPreventive = true,
        MaintenancePlanId = 1,
        DueDate = dueDate,
        Status = status,
        CreatedAt = created,
        ActualStartAt = done?.AddHours(-1),
        CompletedAt = done,
        LaborMinutes = done is null ? 0 : 60,
    };
}
