using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Tests.Domain;

public sealed class MaintenancePlanScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MonthlyPlan_AnchoredAtMonthEnd_DoesNotDriftAfterFebruary()
    {
        var plan = Monthly(new DateOnly(2026, 1, 31));

        plan.AdvanceDueDate();
        Assert.Equal(new DateOnly(2026, 2, 28), plan.NextDueDate);
        plan.AdvanceDueDate();
        Assert.Equal(new DateOnly(2026, 3, 31), plan.NextDueDate);
        plan.AdvanceDueDate();
        Assert.Equal(new DateOnly(2026, 4, 30), plan.NextDueDate);
    }

    [Fact]
    public void MonthlyPlan_InLeapYear_UsesFebruary29()
    {
        var plan = Monthly(new DateOnly(2028, 1, 31));

        Assert.Equal(new DateOnly(2028, 2, 29), plan.NextOccurrenceAfter(new DateOnly(2028, 1, 31)));
    }

    [Theory]
    [InlineData(MaintenancePeriodUnit.Day, 10, "2026-01-05", "2026-01-11")]
    [InlineData(MaintenancePeriodUnit.Week, 2, "2026-01-14", "2026-01-15")]
    [InlineData(MaintenancePeriodUnit.Month, 3, "2026-04-01", "2026-07-01")]
    [InlineData(MaintenancePeriodUnit.Year, 1, "2027-06-01", "2028-01-01")]
    public void NextOccurrenceAfter_ReturnsFirstScheduleDateStrictlyAfter(
        MaintenancePeriodUnit unit, int value, string after, string expected)
    {
        var plan = new MaintenancePlan(1, "Plan", value, unit, Priority.Normal, 30, new DateOnly(2026, 1, 1), Now);

        Assert.Equal(DateOnly.Parse(expected), plan.NextOccurrenceAfter(DateOnly.Parse(after)));
    }

    [Fact]
    public void NextOccurrenceAfter_BeforeAnchor_ReturnsAnchor()
    {
        var plan = Monthly(new DateOnly(2026, 5, 10));

        Assert.Equal(new DateOnly(2026, 5, 10), plan.NextOccurrenceAfter(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void MarkOccurrenceGenerated_OnDueDate_AdvancesOnePeriod()
    {
        var plan = Monthly(new DateOnly(2026, 3, 15));

        var skipped = plan.MarkOccurrenceGenerated(new DateOnly(2026, 3, 15), Now);

        Assert.Equal(0, skipped);
        Assert.Equal(new DateOnly(2026, 4, 15), plan.NextDueDate);
        Assert.False(plan.IsDue(new DateOnly(2026, 3, 15)));
    }

    [Fact]
    public void MarkOccurrenceGenerated_WhenOverdue_SkipsMissedPeriodsAndReportsThem()
    {
        var plan = Monthly(new DateOnly(2026, 1, 10));

        var skipped = plan.MarkOccurrenceGenerated(new DateOnly(2026, 4, 20), Now);

        Assert.Equal(3, skipped);
        Assert.Equal(new DateOnly(2026, 5, 10), plan.NextDueDate);
    }

    [Fact]
    public void MarkOccurrenceGenerated_ForInactiveOrNotDuePlan_IsRejected()
    {
        var inactive = Monthly(new DateOnly(2026, 1, 10));
        inactive.Deactivate();
        var notDue = Monthly(new DateOnly(2026, 6, 1));

        Assert.False(inactive.IsDue(new DateOnly(2026, 2, 1)));
        Assert.Equal("PM-INACTIVE", Assert.Throws<BusinessRuleException>(() =>
            inactive.MarkOccurrenceGenerated(new DateOnly(2026, 2, 1), Now)).RuleCode);
        Assert.Equal("PM-NOT-DUE", Assert.Throws<BusinessRuleException>(() =>
            notDue.MarkOccurrenceGenerated(new DateOnly(2026, 2, 1), Now)).RuleCode);
    }

    [Fact]
    public void RecordCompletion_SetsLastDoneAndMovesPastCompletionDate()
    {
        var plan = Monthly(new DateOnly(2026, 2, 1));
        var completedAt = new DateTimeOffset(2026, 3, 5, 14, 0, 0, TimeSpan.FromHours(3));

        plan.RecordCompletion(completedAt);

        Assert.Equal(completedAt, plan.LastDoneAt);
        Assert.Equal(new DateOnly(2026, 4, 1), plan.NextDueDate);
    }

    [Fact]
    public void RecordCompletion_WhenNextDueIsAlreadyAhead_KeepsSchedule()
    {
        var plan = Monthly(new DateOnly(2026, 2, 1));
        plan.MarkOccurrenceGenerated(new DateOnly(2026, 2, 1), Now);

        plan.RecordCompletion(new DateTimeOffset(2026, 2, 3, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 3, 1), plan.NextDueDate);
    }

    [Fact]
    public void Update_WithNewSchedule_ReanchorsPlan()
    {
        var plan = Monthly(new DateOnly(2026, 1, 31));

        plan.Update("Haftalık yağlama", 1, MaintenancePeriodUnit.Week, Priority.High, 45, new DateOnly(2026, 2, 2), Now.AddDays(1));

        Assert.Equal("Haftalık yağlama", plan.Name);
        Assert.Equal(new DateOnly(2026, 2, 2), plan.AnchorDate);
        Assert.Equal(Priority.High, plan.Priority);
        Assert.Equal(45, plan.EstimatedDurationMinutes);
        Assert.Equal(new DateOnly(2026, 2, 9), plan.NextOccurrenceAfter(new DateOnly(2026, 2, 2)));
    }

    [Fact]
    public void Update_WithOnlyDescriptiveChanges_KeepsAnchor()
    {
        var plan = Monthly(new DateOnly(2026, 1, 31));
        plan.AdvanceDueDate();

        plan.Update("Yeni ad", 1, MaintenancePeriodUnit.Month, Priority.Low, 30, plan.NextDueDate, Now.AddDays(1));

        Assert.Equal(new DateOnly(2026, 1, 31), plan.AnchorDate);
    }

    [Fact]
    public void Update_WithInvalidPeriod_IsRejected()
    {
        var plan = Monthly(new DateOnly(2026, 1, 31));

        Assert.Throws<BusinessRuleException>(() =>
            plan.Update("Plan", 0, MaintenancePeriodUnit.Month, Priority.Low, 30, plan.NextDueDate, Now));
        Assert.Throws<DomainValidationException>(() =>
            plan.Update("Plan", 1, (MaintenancePeriodUnit)99, Priority.Low, 30, plan.NextDueDate, Now));
    }

    [Fact]
    public void Rehydrate_RestoresAnchorAndLastDone()
    {
        var lastDone = Now.AddDays(-3);
        var plan = MaintenancePlan.Rehydrate(
            5, 1, "Plan", 1, MaintenancePeriodUnit.Month, Priority.Normal, 30,
            new DateOnly(2026, 3, 31), true, Now, Now, [1], new DateOnly(2026, 1, 31), lastDone);

        Assert.Equal(new DateOnly(2026, 1, 31), plan.AnchorDate);
        Assert.Equal(lastDone, plan.LastDoneAt);
        Assert.Equal(new DateOnly(2026, 4, 30), plan.NextOccurrenceAfter(plan.NextDueDate));
    }

    [Fact]
    public void Equipment_ReliabilityProfile_IsStored()
    {
        var equipment = new Equipment("EQ-1", "Pres", "Hat 1", Now);

        equipment.SetReliabilityProfile(true, new DateOnly(2027, 1, 1), Now.AddHours(1));

        Assert.True(equipment.IsCritical);
        Assert.Equal(new DateOnly(2027, 1, 1), equipment.WarrantyEndDate);
        Assert.Equal(Now.AddHours(1), equipment.UpdatedAt);
    }

    private static MaintenancePlan Monthly(DateOnly firstDue) =>
        new(1, "Aylık bakım", 1, MaintenancePeriodUnit.Month, Priority.Normal, 60, firstDue, Now);
}
