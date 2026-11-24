using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Maintenance;
using MaintainIQ.Services.WorkOrders;
using NSubstitute;

namespace MaintainIQ.Tests.Services;

public sealed class MaintenancePlanServiceTests
{
    private static readonly DateTimeOffset RunAt = new(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public async Task Generate_ForDuePlan_CreatesPreventiveWorkOrderAndAdvancesPlanInOneTransaction()
    {
        var fixture = new ServiceFixture();
        var plan = Plan(10, new DateOnly(2026, 11, 23));
        Arrange(fixture, plan);

        var result = await Service(fixture).GenerateDueWorkOrdersAsync(RunAt);

        var item = Assert.Single(result.Items);
        Assert.Equal(PlanGenerationOutcome.Created, item.Outcome);
        Assert.Equal(new DateOnly(2026, 11, 23), item.DueDate);
        Assert.Equal(103, item.WorkOrderId);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(new DateOnly(2026, 12, 23), plan.NextDueDate);
        await fixture.WorkOrders.Received(1).AddAsync(
            Arg.Is<PreventiveWorkOrder>(order =>
                order.MaintenancePlanId == 10
                && order.DueDate == new DateOnly(2026, 11, 23)
                && order.EquipmentId == 3
                && order.Priority == Priority.High),
            Arg.Any<CancellationToken>());
        await fixture.MaintenancePlans.Received(1).UpdateAsync(plan, Arg.Any<CancellationToken>());
        await fixture.Audit.Received(2).AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_SecondRunOnSameDay_DoesNotCreateDuplicate()
    {
        var fixture = new ServiceFixture();
        var plan = Plan(10, new DateOnly(2026, 11, 23));
        Arrange(fixture, plan);
        var service = Service(fixture);

        await service.GenerateDueWorkOrdersAsync(RunAt);
        var second = await service.GenerateDueWorkOrdersAsync(RunAt.AddMinutes(5));

        Assert.Equal(0, second.CreatedCount);
        Assert.Empty(second.Items);
        await fixture.WorkOrders.Received(1).AddAsync(Arg.Any<WorkOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_WhenOccurrenceAlreadyExists_HealsPlanWithoutNewWorkOrder()
    {
        var fixture = new ServiceFixture();
        var plan = Plan(10, new DateOnly(2026, 11, 23));
        Arrange(fixture, plan);
        fixture.WorkOrders.ExistsPreventiveOccurrenceAsync(10, new DateOnly(2026, 11, 23), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Service(fixture).GenerateDueWorkOrdersAsync(RunAt);

        Assert.Equal(PlanGenerationOutcome.AlreadyExists, Assert.Single(result.Items).Outcome);
        Assert.Equal(new DateOnly(2026, 12, 23), plan.NextDueDate);
        await fixture.WorkOrders.DidNotReceive().AddAsync(Arg.Any<WorkOrder>(), Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.Received(2).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_ForOverduePlan_CreatesSingleOrderForOldestDueDateAndReportsSkipped()
    {
        var fixture = new ServiceFixture();
        var plan = Plan(10, new DateOnly(2026, 8, 23));
        Arrange(fixture, plan);

        var result = await Service(fixture).GenerateDueWorkOrdersAsync(RunAt);

        var item = Assert.Single(result.Items);
        Assert.Equal(new DateOnly(2026, 8, 23), item.DueDate);
        Assert.Equal(3, item.SkippedOccurrences);
        Assert.Equal(new DateOnly(2026, 12, 23), plan.NextDueDate);
        await fixture.WorkOrders.Received(1).AddAsync(Arg.Any<WorkOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_WhenPlanWasDeactivatedMeanwhile_SkipsIt()
    {
        var fixture = new ServiceFixture();
        var plan = Plan(10, new DateOnly(2026, 11, 1));
        fixture.MaintenancePlans.GetDueAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([plan]);
        fixture.MaintenancePlans.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(plan);
        plan.Deactivate();

        var result = await Service(fixture).GenerateDueWorkOrdersAsync(RunAt);

        Assert.Equal(PlanGenerationOutcome.AlreadyExists, Assert.Single(result.Items).Outcome);
        await fixture.WorkOrders.DidNotReceive().AddAsync(Arg.Any<WorkOrder>(), Arg.Any<CancellationToken>());
        await fixture.MaintenancePlans.DidNotReceive().UpdateAsync(Arg.Any<MaintenancePlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_WhenOnePlanFails_ContinuesWithOthers()
    {
        var fixture = new ServiceFixture();
        var broken = Plan(10, new DateOnly(2026, 11, 20));
        var healthy = Plan(11, new DateOnly(2026, 11, 21));
        fixture.MaintenancePlans.GetDueAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([broken, healthy]);
        fixture.MaintenancePlans.GetByIdAsync(10, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MaintenancePlan?>(new BusinessRuleException("X", "bozuk plan")));
        fixture.MaintenancePlans.GetByIdAsync(11, Arg.Any<CancellationToken>()).Returns(healthy);

        var result = await Service(fixture).GenerateDueWorkOrdersAsync(RunAt);

        Assert.Equal(1, result.FailedCount);
        Assert.Equal("bozuk plan", result.Items[0].Message);
        Assert.Equal(1, result.CreatedCount);
        await fixture.UnitOfWork.Received(1).RollbackAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Generate_RequiresSignedInUser()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.SignOut();

        await Assert.ThrowsAsync<PermissionDeniedException>(() => Service(fixture).GenerateDueWorkOrdersAsync(RunAt));
    }

    [Fact]
    public async Task Create_ForActiveEquipment_PersistsPlanWithAudit()
    {
        var fixture = new ServiceFixture();
        fixture.Equipment.GetByIdAsync(3, Arg.Any<CancellationToken>())
            .Returns(new Equipment("EQ-3", "Kompresör", "Hat 2", ServiceFixture.Now));

        var id = await Service(fixture).CreateAsync(new CreateMaintenancePlanCommand(
            3, "Aylık filtre", 1, MaintenancePeriodUnit.Month, Priority.Normal, 45, new DateOnly(2026, 12, 1)));

        Assert.Equal(108, id);
        await fixture.MaintenancePlans.Received(1).AddAsync(
            Arg.Is<MaintenancePlan>(plan => plan.AnchorDate == new DateOnly(2026, 12, 1) && plan.Name == "Aylık filtre"),
            Arg.Any<CancellationToken>());
        await fixture.Audit.Received(1).AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ForRetiredOrMissingEquipment_IsRejected()
    {
        var fixture = new ServiceFixture();
        var retired = new Equipment("EQ-4", "Eski pres", "Depo", ServiceFixture.Now);
        retired.Retire();
        fixture.Equipment.GetByIdAsync(4, Arg.Any<CancellationToken>()).Returns(retired);
        var service = Service(fixture);

        var rule = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Command(4)));
        Assert.Equal("PM-RETIRED", rule.RuleCode);
        await Assert.ThrowsAsync<MaintainIQ.Services.Common.EntityNotFoundException>(() => service.CreateAsync(Command(99)));
        await fixture.MaintenancePlans.DidNotReceive().AddAsync(Arg.Any<MaintenancePlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Operator_CannotManagePlans()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.SignIn(2, 8, "operator", UserRole.Operator);
        var service = Service(fixture);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.CreateAsync(Command(3)));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.DeactivateAsync(10));
        fixture.Factory.DidNotReceive().Create();
    }

    [Fact]
    public async Task UpdateActivateDeactivate_PersistChangesWithAudit()
    {
        var fixture = new ServiceFixture();
        var plan = Plan(10, new DateOnly(2026, 12, 1));
        fixture.MaintenancePlans.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(plan);
        var service = Service(fixture);

        await service.UpdateAsync(new UpdateMaintenancePlanCommand(
            10, "İki aylık bakım", 2, MaintenancePeriodUnit.Month, Priority.Low, 90, new DateOnly(2026, 12, 15)));
        Assert.Equal(new DateOnly(2026, 12, 15), plan.AnchorDate);
        await service.DeactivateAsync(10);
        Assert.False(plan.IsActive);
        await service.ActivateAsync(10);
        Assert.True(plan.IsActive);

        await fixture.MaintenancePlans.Received(3).UpdateAsync(plan, Arg.Any<CancellationToken>());
        await fixture.Audit.Received(3).AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Queries_ReturnRepositoryData()
    {
        var fixture = new ServiceFixture();
        var plan = Plan(10, new DateOnly(2026, 12, 1));
        fixture.MaintenancePlans.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(plan);
        fixture.MaintenancePlans.GetAllAsync(Arg.Any<CancellationToken>()).Returns([plan]);
        fixture.MaintenancePlans.GetForEquipmentAsync(3, Arg.Any<CancellationToken>()).Returns([plan]);
        var service = Service(fixture);

        Assert.Same(plan, await service.GetAsync(10));
        Assert.Single(await service.GetAllAsync());
        Assert.Single(await service.GetForEquipmentAsync(3));
        await Assert.ThrowsAsync<MaintainIQ.Services.Common.EntityNotFoundException>(() => service.GetAsync(77));
    }

    [Fact]
    public async Task CompletingPreventiveWorkOrder_UpdatesPlanLastDoneAndNextDueInSameTransaction()
    {
        var fixture = new ServiceFixture();
        var plan = Plan(10, new DateOnly(2026, 11, 1));
        fixture.MaintenancePlans.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(plan);
        var order = PreventiveWorkOrder.Rehydrate(
            40, "WO-PM-40", 3, 10, new DateOnly(2026, 11, 1), Priority.High, "Aylık yağlama", null,
            WorkOrderStatus.InProgress, 7, ServiceFixture.Now.AddHours(-2), null, null, 0,
            ServiceFixture.Now.AddDays(-1), ServiceFixture.Now.AddHours(-2), [1]);
        fixture.WorkOrders.GetWithPartsAsync(40, Arg.Any<CancellationToken>()).Returns(order);
        fixture.Equipment.GetByIdAsync(3, Arg.Any<CancellationToken>())
            .Returns(new Equipment("EQ-3", "Kompresör", "Hat 2", ServiceFixture.Now.AddDays(-30)));
        var service = new WorkOrderService(fixture.Factory, fixture.CurrentUser, fixture.Permissions, fixture.Clock);

        await service.CompleteAsync(40, 45);

        Assert.Equal(ServiceFixture.Now, plan.LastDoneAt);
        Assert.Equal(new DateOnly(2026, 12, 1), plan.NextDueDate);
        await fixture.MaintenancePlans.Received(1).UpdateAsync(plan, Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private static void Arrange(ServiceFixture fixture, MaintenancePlan plan)
    {
        fixture.MaintenancePlans.GetDueAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => plan.IsDue(DateOnly.FromDateTime(RunAt.DateTime)) ? [plan] : []);
        fixture.MaintenancePlans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
    }

    private static MaintenancePlanService Service(ServiceFixture fixture) => new(
        fixture.Factory, fixture.CurrentUser, fixture.Permissions, fixture.Clock, new WorkOrderNumberGenerator());

    private static CreateMaintenancePlanCommand Command(int equipmentId) =>
        new(equipmentId, "Plan", 1, MaintenancePeriodUnit.Month, Priority.Normal, 30, new DateOnly(2026, 12, 1));

    private static MaintenancePlan Plan(int id, DateOnly nextDue) => MaintenancePlan.Rehydrate(
        id, 3, "Aylık yağlama", 1, MaintenancePeriodUnit.Month, Priority.High, 60, nextDue, true,
        ServiceFixture.Now.AddMonths(-6), ServiceFixture.Now.AddMonths(-6), [1, 2]);
}
