using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Tests.Domain;

public sealed class WorkOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CorrectiveWorkOrder_StartsCreatedAndKeepsFailureLink()
    {
        var order = CreateCorrective();

        Assert.Equal(WorkOrderStatus.Created, order.Status);
        Assert.Equal(10, order.FailureId);
    }

    [Fact]
    public void CorrectiveWorkOrder_RejectsInvalidFailure()
    {
        Assert.Throws<DomainValidationException>(() =>
            new CorrectiveWorkOrder("WO-1", 1, 0, Priority.Normal, "Onarım", Now));
    }

    [Fact]
    public void WorkOrderPart_CopiesUnitCostAndCalculatesTotal()
    {
        var part = new WorkOrderPart(2, 3m, new Money(25m), Now);

        Assert.Equal(new Money(75m), part.TotalCost);
    }

    [Fact]
    public void CalculateCost_SumsPartSnapshots()
    {
        var order = CreateCorrective();
        order.AddPart(new WorkOrderPart(2, 2m, new Money(25m), Now));
        order.AddPart(new WorkOrderPart(3, 1m, new Money(10m), Now));

        var cost = order.CalculateCost(new Money(300m));

        Assert.Equal(new Money(60m), cost);
    }

    [Fact]
    public void PreventiveWorkOrder_KeepsPlanAndDueDate()
    {
        var dueDate = new DateOnly(2026, 12, 1);
        var order = new PreventiveWorkOrder("PM-1", 1, 5, dueDate, Priority.Normal, "Planlı bakım", Now);

        Assert.Equal(5, order.MaintenancePlanId);
        Assert.Equal(dueDate, order.DueDate);
    }

    private static CorrectiveWorkOrder CreateCorrective() =>
        new("WO-1", 1, 10, Priority.High, "Pompa onarımı", Now);
}

public sealed class FailureTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_StartsOpen()
    {
        var failure = new Failure(1, 2, FailureSeverity.Critical, "Motor durdu", 3, Now);

        Assert.Equal(FailureStatus.Open, failure.Status);
    }

    [Fact]
    public void Resolve_StoresResolutionTime()
    {
        var failure = new Failure(1, 2, FailureSeverity.High, "Titreşim", 3, Now);

        failure.Resolve(Now.AddHours(2));

        Assert.Equal(FailureStatus.Resolved, failure.Status);
        Assert.Equal(Now.AddHours(2), failure.ResolvedAt);
    }

    [Fact]
    public void Resolve_RejectsTimeBeforeReport()
    {
        var failure = new Failure(1, 2, FailureSeverity.High, "Titreşim", 3, Now);

        Assert.Throws<DomainValidationException>(() => failure.Resolve(Now.AddMinutes(-1)));
    }
}
