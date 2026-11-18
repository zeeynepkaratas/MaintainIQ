using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Tests.Domain;

public sealed class SparePartTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receive_IncreasesStockAndUpdatesCost()
    {
        var part = Create(5m);

        part.Receive(3m, new Money(120m));

        Assert.Equal(8m, part.StockQuantity);
        Assert.Equal(new Money(120m), part.UnitCost);
    }

    [Fact]
    public void Receive_RejectsDifferentCurrency()
    {
        var part = Create(5m);

        Assert.Throws<DomainValidationException>(() => part.Receive(1m, new Money(10m, "USD")));
    }

    [Fact]
    public void Consume_ReturnsCostSnapshotAndReducesStock()
    {
        var part = Create(5m);

        var snapshot = part.Consume(2m);

        Assert.Equal(3m, part.StockQuantity);
        Assert.Equal(new Money(100m), snapshot);
    }

    [Fact]
    public void Consume_RejectsInsufficientStock()
    {
        var part = Create(1m);

        var error = Assert.Throws<BusinessRuleException>(() => part.Consume(2m));

        Assert.Equal("BR-02", error.RuleCode);
    }

    [Fact]
    public void Adjust_RequiresReason()
    {
        var part = Create(5m);

        Assert.Throws<DomainValidationException>(() => part.Adjust(4m, " "));
    }

    [Fact]
    public void BelowMinimum_ReflectsCurrentStock()
    {
        var part = Create(1m);

        Assert.True(part.IsBelowMinimum);
    }

    private static SparePart Create(decimal stock) =>
        new("flt-01", "Filtre", stock, 2m, new Money(100m), Now);
}

public sealed class MaintenancePlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_RejectsNonPositivePeriod()
    {
        Assert.Throws<BusinessRuleException>(() =>
            new MaintenancePlan(1, "Aylık bakım", 0, MaintenancePeriodUnit.Month, Priority.Normal, 60, new DateOnly(2026, 12, 1), Now));
    }

    [Fact]
    public void CalculateNextDue_AddsSelectedPeriod()
    {
        var plan = Create();

        var next = plan.CalculateNextDue(new DateOnly(2026, 11, 30));

        Assert.Equal(new DateOnly(2026, 12, 30), next);
    }

    [Fact]
    public void AdvanceDueDate_UpdatesPlan()
    {
        var plan = Create();

        plan.AdvanceDueDate();

        Assert.Equal(new DateOnly(2027, 1, 1), plan.NextDueDate);
    }

    private static MaintenancePlan Create() =>
        new(1, "Aylık bakım", 1, MaintenancePeriodUnit.Month, Priority.Normal, 60, new DateOnly(2026, 12, 1), Now);
}
