using MaintainIQ.Data.ReadModels;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;
using MaintainIQ.Services.Equipment;
using MaintainIQ.Services.Failures;
using MaintainIQ.Services.Inventory;
using MaintainIQ.Services.Technicians;
using MaintainIQ.Services.WorkOrders;
using NSubstitute;

namespace MaintainIQ.Tests.Services;

public sealed class ApplicationServiceTests
{
    [Fact]
    public async Task EquipmentService_CoversCrudHierarchyStatusAndQueries()
    {
        var f = new ServiceFixture();
        var parent = Equipment(2, "PARENT");
        var updateTarget = Equipment(3, "UPDATE");
        var moveTarget = Equipment(4, "MOVE");
        var deleteTarget = Equipment(5, "DELETE");
        var statusTarget = Equipment(6, "STATUS");
        f.Equipment.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(parent);
        f.Equipment.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(updateTarget);
        f.Equipment.GetByIdAsync(4, Arg.Any<CancellationToken>()).Returns(moveTarget);
        f.Equipment.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(deleteTarget);
        f.Equipment.GetByIdAsync(6, Arg.Any<CancellationToken>()).Returns(statusTarget);
        f.Equipment.GetDescendantsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EquipmentTreeItemDto>());
        f.Equipment.ExistsOpenWorkAsync(5, Arg.Any<CancellationToken>()).Returns(false);
        var tree = new[] { new EquipmentTreeItemDto { Id = 2, Code = "PARENT" } };
        f.Equipment.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        f.Equipment.SearchAsync("pompa", Arg.Any<CancellationToken>()).Returns(new[] { parent });
        var service = new EquipmentService(f.Factory, f.CurrentUser, f.Permissions, f.Clock);

        var createdId = await service.CreateAsync(new("NEW", "Yeni", "Hat", 2));
        await service.UpdateAsync(new(3, "Güncel", "Hat 2"));
        await service.MoveAsync(4, 2);
        await service.DeleteAsync(5);
        await service.ChangeStatusAsync(6, EquipmentStatus.Faulty);
        var actualTree = await service.GetTreeAsync();
        var search = await service.SearchAsync("pompa");

        Assert.Equal(101, createdId);
        Assert.Equal("Güncel", updateTarget.Name);
        Assert.Equal(2, moveTarget.ParentId);
        Assert.True(deleteTarget.IsDeleted);
        Assert.Equal(EquipmentStatus.Faulty, statusTarget.Status);
        Assert.Same(tree, actualTree);
        Assert.Single(search);
        await f.Audit.Received(5).AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailureService_CoversCriticalReportLifecycleAndWorkOrderCreation()
    {
        var f = new ServiceFixture();
        var equipment = Equipment(20, "EQ-20");
        var open = Failure(1, FailureStatus.Open, FailureSeverity.High);
        var inReview = Failure(2, FailureStatus.InReview, FailureSeverity.Medium);
        var critical = Failure(3, FailureStatus.Open, FailureSeverity.Critical);
        f.Equipment.GetByIdAsync(20, Arg.Any<CancellationToken>()).Returns(equipment);
        f.Failures.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(open);
        f.Failures.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(inReview);
        f.Failures.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(critical);
        var service = new FailureService(
            f.Factory, f.CurrentUser, f.Permissions, f.Clock, new FixedNumberGenerator());

        var reportId = await service.ReportAsync(new(20, 1, FailureSeverity.Critical, "Kritik arıza"));
        await service.AcknowledgeAsync(1);
        await service.ResolveAsync(2);
        var workOrderId = await service.CreateWorkOrderAsync(3, Priority.Low);

        Assert.Equal(102, reportId);
        Assert.Equal(EquipmentStatus.Faulty, equipment.Status);
        Assert.Equal(FailureStatus.InReview, open.Status);
        Assert.Equal(FailureStatus.Resolved, inReview.Status);
        Assert.Equal(103, workOrderId);
        await f.WorkOrders.Received(1).AddAsync(
            Arg.Is<WorkOrder>(order => order.Priority == Priority.High && order.Number == "WO-FIXED"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InventoryService_CoversReceiptAdjustmentAlertsAndHistory()
    {
        var f = new ServiceFixture();
        var receivePart = SparePart(1, 5m);
        var adjustPart = SparePart(2, 8m);
        f.SpareParts.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(receivePart);
        f.SpareParts.GetForUpdateAsync(2, Arg.Any<CancellationToken>()).Returns(adjustPart);
        f.SpareParts.GetBelowMinimumAsync(Arg.Any<CancellationToken>()).Returns(new[] { receivePart });
        var movements = new[]
        {
            new StockMovement(1, StockMovementType.Receipt, 1, 6, new Money(10), ServiceFixture.Now, "Giriş"),
        };
        f.StockMovements.GetAllAsync(Arg.Any<CancellationToken>()).Returns(movements);
        var service = new InventoryService(f.Factory, f.CurrentUser, f.Permissions, f.Clock);

        await service.ReceiveAsync(1, 3m, new Money(12m));
        await service.AdjustAsync(2, 4m, "Sayım farkı");
        var low = await service.GetBelowMinimumAsync();
        var history = await service.GetMovementsAsync();

        Assert.Equal(8m, receivePart.StockQuantity);
        Assert.Equal(4m, adjustPart.StockQuantity);
        Assert.Single(low);
        Assert.Single(history);
        await f.StockMovements.Received(2).AddAsync(
            Arg.Any<StockMovement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TechnicianService_CoversCrudWorkloadAndAvailability()
    {
        var f = new ServiceFixture();
        var updateTarget = Technician(1, true);
        var deactivateTarget = Technician(2, true);
        f.Technicians.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(updateTarget);
        f.Technicians.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(deactivateTarget);
        f.Technicians.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { updateTarget, deactivateTarget });
        var workload = new[] { new WorkOrderListItemDto { Id = 10, Number = "WO-10" } };
        f.WorkOrders.SearchAsync(Arg.Any<WorkOrderFilter>(), Arg.Any<CancellationToken>()).Returns(workload);
        f.WorkOrders.HasScheduleConflictAsync(
            1, Arg.Any<DateRange>(), null, Arg.Any<CancellationToken>()).Returns(false);
        var service = new TechnicianService(f.Factory, f.CurrentUser, f.Permissions, f.Clock);

        var id = await service.CreateAsync(new("T-3", "Yeni", "Teknisyen", "Elektrik"));
        await service.UpdateAsync(1, "Güncel", "Teknisyen");
        await service.DeactivateAsync(2);
        var all = await service.GetAllAsync();
        var actualWorkload = await service.GetWorkloadAsync(1);
        var available = await service.IsAvailableAsync(
            1, new DateRange(ServiceFixture.Now, ServiceFixture.Now.AddHours(2)));

        Assert.Equal(106, id);
        Assert.Equal("Güncel", updateTarget.FirstName);
        Assert.False(deactivateTarget.IsActive);
        Assert.Equal(2, all.Count);
        Assert.Single(actualWorkload);
        Assert.True(available);
    }

    [Fact]
    public async Task WorkOrderService_CoversLifecycleCommands()
    {
        var f = new ServiceFixture();
        var assign = Order(1, WorkOrderStatus.Created);
        var start = Order(2, WorkOrderStatus.Assigned, 7);
        var request = Order(3, WorkOrderStatus.InProgress, 7);
        var resume = Order(4, WorkOrderStatus.WaitingForParts, 7);
        var addPart = Order(5, WorkOrderStatus.InProgress, 7);
        var close = Order(6, WorkOrderStatus.Completed, 7, completedAt: ServiceFixture.Now.AddMinutes(-10));
        var reopen = Order(7, WorkOrderStatus.Completed, 7, completedAt: ServiceFixture.Now.AddMinutes(-10));
        var cancel = Order(8, WorkOrderStatus.Created);
        f.WorkOrders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(assign);
        f.WorkOrders.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(start);
        f.WorkOrders.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(request);
        f.WorkOrders.GetByIdAsync(4, Arg.Any<CancellationToken>()).Returns(resume);
        f.WorkOrders.GetWithPartsAsync(5, Arg.Any<CancellationToken>()).Returns(addPart);
        f.WorkOrders.GetByIdAsync(6, Arg.Any<CancellationToken>()).Returns(close);
        f.WorkOrders.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(reopen);
        f.WorkOrders.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(cancel);
        f.Technicians.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(Technician(9, true));
        f.WorkOrders.HasScheduleConflictAsync(
            9, Arg.Any<DateRange>(), 1, Arg.Any<CancellationToken>()).Returns(false);
        f.SpareParts.GetByIdAsync(11, Arg.Any<CancellationToken>()).Returns(SparePart(11, 5m));
        var service = new WorkOrderService(f.Factory, f.CurrentUser, f.Permissions, f.Clock);

        await service.AssignAsync(1, 9, new(ServiceFixture.Now.AddHours(1), ServiceFixture.Now.AddHours(2)), false);
        await service.StartAsync(2);
        await service.RequestPartsAsync(3, "Parça bekleniyor");
        await service.ResumeAsync(4);
        await service.AddPartAsync(5, 11, 2m);
        await service.CloseAsync(6);
        await service.ReopenAsync(7, "Ek kontrol");
        await service.CancelAsync(8, "İhtiyaç kalmadı");

        Assert.Equal(WorkOrderStatus.Assigned, assign.Status);
        Assert.Equal(WorkOrderStatus.InProgress, start.Status);
        Assert.Equal(WorkOrderStatus.WaitingForParts, request.Status);
        Assert.Equal(WorkOrderStatus.InProgress, resume.Status);
        Assert.Single(addPart.Parts);
        Assert.Equal(WorkOrderStatus.Closed, close.Status);
        Assert.Equal(WorkOrderStatus.InProgress, reopen.Status);
        Assert.Equal(WorkOrderStatus.Cancelled, cancel.Status);
    }

    private static MaintainIQ.Domain.Entities.Equipment Equipment(int id, string code) =>
        MaintainIQ.Domain.Entities.Equipment.Rehydrate(
            id, null, code, $"{code} adı", "Hat", $"qr-{id}", EquipmentStatus.Operational,
            false, ServiceFixture.Now.AddDays(-1), ServiceFixture.Now.AddDays(-1), [1]);

    private static MaintainIQ.Domain.Entities.Failure Failure(
        int id,
        FailureStatus status,
        FailureSeverity severity) =>
        MaintainIQ.Domain.Entities.Failure.Rehydrate(
            id, 20, 1, severity, status, "Arıza", 7, ServiceFixture.Now.AddHours(-2), null,
            ServiceFixture.Now.AddHours(-2), ServiceFixture.Now.AddHours(-2), [1]);

    private static SparePart SparePart(int id, decimal stock) =>
        MaintainIQ.Domain.Entities.SparePart.Rehydrate(
            id, null, $"P-{id}", $"Parça {id}", stock, 2m, 10m, "TRY",
            ServiceFixture.Now.AddDays(-1), ServiceFixture.Now.AddDays(-1), [1]);

    private static Technician Technician(int id, bool active) =>
        MaintainIQ.Domain.Entities.Technician.Rehydrate(
            id, $"T-{id}", "Test", "Teknisyen", "Mekanik", active,
            ServiceFixture.Now.AddDays(-1), ServiceFixture.Now.AddDays(-1), [1]);

    private static CorrectiveWorkOrder Order(
        int id,
        WorkOrderStatus status,
        int? technicianId = null,
        DateTimeOffset? completedAt = null) =>
        CorrectiveWorkOrder.Rehydrate(
            id, $"WO-{id}", 20, 30, Priority.Normal, "Test işi", null, status,
            technicianId,
            status is WorkOrderStatus.InProgress or WorkOrderStatus.WaitingForParts or WorkOrderStatus.Completed
                ? ServiceFixture.Now.AddHours(-1)
                : null,
            completedAt,
            null,
            completedAt is null ? 0 : 30,
            ServiceFixture.Now.AddHours(-2),
            ServiceFixture.Now.AddHours(-1),
            [1]);

    private sealed class FixedNumberGenerator : IWorkOrderNumberGenerator
    {
        public string Next(DateTimeOffset now) => "WO-FIXED";
    }
}
