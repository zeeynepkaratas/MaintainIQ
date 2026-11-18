using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Tests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_NormalizesCurrency()
    {
        var money = new Money(125.50m, "try");

        Assert.Equal(125.50m, money.Amount);
        Assert.Equal("TRY", money.Currency);
    }

    [Fact]
    public void Constructor_RejectsNegativeAmount() =>
        Assert.Throws<DomainValidationException>(() => new Money(-0.01m));

    [Fact]
    public void Constructor_RejectsInvalidCurrency() =>
        Assert.Throws<DomainValidationException>(() => new Money(10m, "TL"));

    [Fact]
    public void Add_SumsSameCurrency()
    {
        var result = new Money(10m).Add(new Money(2.5m));

        Assert.Equal(new Money(12.5m), result);
    }

    [Fact]
    public void Add_RejectsDifferentCurrencies() =>
        Assert.Throws<DomainValidationException>(() => new Money(10m, "TRY").Add(new Money(5m, "USD")));

    [Fact]
    public void Multiply_RejectsNegativeMultiplier() =>
        Assert.Throws<DomainValidationException>(() => new Money(10m).Multiply(-1m));
}

public sealed class DateRangeTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_RejectsNonPositiveRange() =>
        Assert.Throws<DomainValidationException>(() => new DateRange(Start, Start));

    [Fact]
    public void Duration_ReturnsDifference()
    {
        var range = new DateRange(Start, Start.AddHours(2));

        Assert.Equal(TimeSpan.FromHours(2), range.Duration);
    }

    [Fact]
    public void Overlaps_DetectsIntersection()
    {
        var first = new DateRange(Start, Start.AddHours(2));
        var second = new DateRange(Start.AddHours(1), Start.AddHours(3));

        Assert.True(first.Overlaps(second));
    }

    [Fact]
    public void Overlaps_TouchingRangesDoNotOverlap()
    {
        var first = new DateRange(Start, Start.AddHours(1));
        var second = new DateRange(Start.AddHours(1), Start.AddHours(2));

        Assert.False(first.Overlaps(second));
    }

    [Fact]
    public void Contains_UsesExclusiveEnd()
    {
        var range = new DateRange(Start, Start.AddHours(1));

        Assert.True(range.Contains(Start));
        Assert.False(range.Contains(Start.AddHours(1)));
    }
}
