using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Tests.Domain;

public sealed class WorkOrderStateMachineTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateRange PlannedWindow = new(CreatedAt.AddHours(1), CreatedAt.AddHours(3));

    [Fact]
    public void Assign_ActiveTechnicianMovesOrderToAssigned()
    {
        var order = CreateOrder();

        order.Assign(7, true, PlannedWindow, false, false, 2, CreatedAt.AddMinutes(1));

        Assert.Equal(WorkOrderStatus.Assigned, order.Status);
        Assert.Equal(7, order.AssignedTechnicianId);
        Assert.Equal(PlannedWindow, order.PlannedWindow);
        AssertHistory(order, WorkOrderStatus.Created, WorkOrderStatus.Assigned);
    }

    [Fact]
    public void Assign_InactiveTechnicianIsRejected()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateOrder().Assign(7, false, PlannedWindow, false, false, 2, CreatedAt.AddMinutes(1)));

        Assert.Equal("BR-03", error.RuleCode);
    }

    [Fact]
    public void Assign_UnapprovedScheduleConflictIsRejected()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateOrder().Assign(7, true, PlannedWindow, true, false, 2, CreatedAt.AddMinutes(1)));

        Assert.Equal("BR-12", error.RuleCode);
    }

    [Fact]
    public void Reassign_ChangesTechnicianAndRecordsSelfTransition()
    {
        var order = CreateAssignedOrder();

        order.Assign(8, true, PlannedWindow, true, true, 2, CreatedAt.AddMinutes(2));

        Assert.Equal(8, order.AssignedTechnicianId);
        AssertHistory(order, WorkOrderStatus.Assigned, WorkOrderStatus.Assigned);
    }

    [Fact]
    public void Unassign_ReturnsOrderToCreated()
    {
        var order = CreateAssignedOrder();

        order.Unassign(2, CreatedAt.AddMinutes(2));

        Assert.Equal(WorkOrderStatus.Created, order.Status);
        Assert.Null(order.AssignedTechnicianId);
    }

    [Fact]
    public void Unassign_FromCreatedIsRejected() =>
        AssertStateError(() => CreateOrder().Unassign(2, CreatedAt.AddMinutes(1)));

    [Fact]
    public void Start_AssignedTechnicianMovesOrderToInProgress()
    {
        var order = CreateAssignedOrder();

        order.Start(7, false, CreatedAt.AddMinutes(2));

        Assert.Equal(WorkOrderStatus.InProgress, order.Status);
        Assert.Equal(CreatedAt.AddMinutes(2), order.ActualStartAt);
    }

    [Fact]
    public void Start_OtherTechnicianIsRejected()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateAssignedOrder().Start(8, false, CreatedAt.AddMinutes(2)));

        Assert.Equal("BR-19", error.RuleCode);
    }

    [Fact]
    public void Start_ElevatedUserMayStartAssignedOrder()
    {
        var order = CreateAssignedOrder();

        order.Start(2, true, CreatedAt.AddMinutes(2));

        Assert.Equal(WorkOrderStatus.InProgress, order.Status);
    }

    [Fact]
    public void Start_FromCreatedIsRejected() =>
        AssertStateError(() => CreateOrder().Start(7, false, CreatedAt.AddMinutes(1)));

    [Fact]
    public void RequestParts_MovesOrderToWaitingAndStoresReason()
    {
        var order = CreateInProgressOrder();

        order.RequestParts("Rulman bekleniyor", 7, CreatedAt.AddMinutes(3));

        Assert.Equal(WorkOrderStatus.WaitingForParts, order.Status);
        Assert.Equal("Rulman bekleniyor", order.StatusHistory.Last().Reason);
    }

    [Fact]
    public void RequestParts_RequiresReason()
    {
        Assert.Throws<DomainValidationException>(() =>
            CreateInProgressOrder().RequestParts(" ", 7, CreatedAt.AddMinutes(3)));
    }

    [Fact]
    public void Resume_ReturnsWaitingOrderToInProgress()
    {
        var order = CreateWaitingOrder();

        order.Resume(7, CreatedAt.AddMinutes(4));

        Assert.Equal(WorkOrderStatus.InProgress, order.Status);
    }

    [Fact]
    public void Complete_ValidDataMovesOrderToCompleted()
    {
        var order = CreateInProgressOrder();

        order.Complete(45, true, 7, false, CreatedAt.AddMinutes(3));

        Assert.Equal(WorkOrderStatus.Completed, order.Status);
        Assert.Equal(45, order.LaborMinutes);
        Assert.Equal(CreatedAt.AddMinutes(3), order.CompletedAt);
    }

    [Fact]
    public void Complete_RequiresPositiveLabor()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateInProgressOrder().Complete(0, true, 7, false, CreatedAt.AddMinutes(3)));

        Assert.Equal("BR-04", error.RuleCode);
    }

    [Fact]
    public void Complete_RequiresSufficientStock()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateInProgressOrder().Complete(30, false, 7, false, CreatedAt.AddMinutes(3)));

        Assert.Equal("BR-02", error.RuleCode);
    }

    [Fact]
    public void Complete_OtherTechnicianIsRejected()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateInProgressOrder().Complete(30, true, 8, false, CreatedAt.AddMinutes(3)));

        Assert.Equal("BR-19", error.RuleCode);
    }

    [Fact]
    public void Reopen_WithSupervisorAndReasonReturnsToInProgress()
    {
        var order = CreateCompletedOrder();

        order.Reopen("Kontrol başarısız", UserRole.Supervisor, 2, CreatedAt.AddMinutes(4));

        Assert.Equal(WorkOrderStatus.InProgress, order.Status);
        Assert.Null(order.CompletedAt);
        Assert.Equal("Kontrol başarısız", order.StatusHistory.Last().Reason);
    }

    [Fact]
    public void Reopen_RequiresReason()
    {
        Assert.Throws<DomainValidationException>(() =>
            CreateCompletedOrder().Reopen(" ", UserRole.Supervisor, 2, CreatedAt.AddMinutes(4)));
    }

    [Fact]
    public void Reopen_TechnicianIsRejected()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateCompletedOrder().Reopen("Tekrar", UserRole.Technician, 7, CreatedAt.AddMinutes(4)));

        Assert.Equal("BR-06", error.RuleCode);
    }

    [Fact]
    public void Close_SupervisorMovesOrderToTerminalState()
    {
        var order = CreateCompletedOrder();

        order.Close(UserRole.Supervisor, 2, CreatedAt.AddMinutes(4));

        Assert.Equal(WorkOrderStatus.Closed, order.Status);
        Assert.True(order.IsTerminal);
        Assert.Equal(CreatedAt.AddMinutes(4), order.ClosedAt);
    }

    [Fact]
    public void Close_TechnicianIsRejected()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateCompletedOrder().Close(UserRole.Technician, 7, CreatedAt.AddMinutes(4)));

        Assert.Equal("BR-06", error.RuleCode);
    }

    [Fact]
    public void Cancel_CreatedOrderStoresRequiredReason()
    {
        var order = CreateOrder();

        order.Cancel("İhtiyaç kalmadı", UserRole.Admin, 1, CreatedAt.AddMinutes(1));

        Assert.Equal(WorkOrderStatus.Cancelled, order.Status);
        Assert.True(order.IsTerminal);
        Assert.Equal("İhtiyaç kalmadı", order.StatusHistory.Last().Reason);
    }

    [Fact]
    public void Cancel_RequiresReason()
    {
        Assert.Throws<DomainValidationException>(() =>
            CreateOrder().Cancel("", UserRole.Admin, 1, CreatedAt.AddMinutes(1)));
    }

    [Fact]
    public void Cancel_TechnicianIsRejected()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            CreateOrder().Cancel("İptal", UserRole.Technician, 7, CreatedAt.AddMinutes(1)));

        Assert.Equal("BR-06", error.RuleCode);
    }

    [Fact]
    public void Cancel_CompletedOrderIsRejected() =>
        AssertStateError(() => CreateCompletedOrder().Cancel("İptal", UserRole.Admin, 1, CreatedAt.AddMinutes(4)));

    [Fact]
    public void ClosedOrder_CannotAcceptParts()
    {
        var order = CreateCompletedOrder();
        order.Close(UserRole.Admin, 1, CreatedAt.AddMinutes(4));

        var error = Assert.Throws<BusinessRuleException>(() =>
            order.AddPart(new WorkOrderPart(1, 1, new Money(10), CreatedAt.AddMinutes(5))));

        Assert.Equal("BR-01", error.RuleCode);
    }

    [Fact]
    public void ActionTime_BeforeCreationIsRejected()
    {
        Assert.Throws<DomainValidationException>(() =>
            CreateOrder().Cancel("İptal", UserRole.Admin, 1, CreatedAt.AddSeconds(-1)));
    }

    private static CorrectiveWorkOrder CreateOrder() =>
        new("WO-STATE-1", 1, 1, Priority.High, "Durum testi", CreatedAt);

    private static CorrectiveWorkOrder CreateAssignedOrder()
    {
        var order = CreateOrder();
        order.Assign(7, true, PlannedWindow, false, false, 2, CreatedAt.AddMinutes(1));
        return order;
    }

    private static CorrectiveWorkOrder CreateInProgressOrder()
    {
        var order = CreateAssignedOrder();
        order.Start(7, false, CreatedAt.AddMinutes(2));
        return order;
    }

    private static CorrectiveWorkOrder CreateWaitingOrder()
    {
        var order = CreateInProgressOrder();
        order.RequestParts("Parça bekleniyor", 7, CreatedAt.AddMinutes(3));
        return order;
    }

    private static CorrectiveWorkOrder CreateCompletedOrder()
    {
        var order = CreateInProgressOrder();
        order.Complete(30, true, 7, false, CreatedAt.AddMinutes(3));
        return order;
    }

    private static void AssertStateError(Action action)
    {
        var error = Assert.Throws<BusinessRuleException>(action);
        Assert.Equal("WO-STATE", error.RuleCode);
    }

    private static void AssertHistory(
        WorkOrder order,
        WorkOrderStatus from,
        WorkOrderStatus to)
    {
        var entry = order.StatusHistory.Last();
        Assert.Equal(from, entry.FromStatus);
        Assert.Equal(to, entry.ToStatus);
    }
}
