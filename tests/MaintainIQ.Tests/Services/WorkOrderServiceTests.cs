using MaintainIQ.Data.Repositories;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using MaintainIQ.Services.WorkOrders;
using NSubstitute;

namespace MaintainIQ.Tests.Services;

public sealed class WorkOrderServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompleteAsync_CommitsStockMovementStatusEquipmentAndAuditTogether()
    {
        var fixture = new WorkOrderFixture();

        await fixture.Service.CompleteAsync(10, 90);

        Assert.Equal(WorkOrderStatus.Completed, fixture.Order.Status);
        Assert.Equal(3m, fixture.Part.StockQuantity);
        Assert.Equal(EquipmentStatus.Operational, fixture.Equipment.Status);
        await fixture.StockMovements.Received(1).AddAsync(
            Arg.Is<StockMovement>(movement =>
                movement.WorkOrderId == 10
                && movement.Quantity == 2m
                && movement.ResultingQuantity == 3m),
            Arg.Any<CancellationToken>());
        await fixture.WorkOrders.Received(1).AddStatusHistoryAsync(
            10,
            Arg.Is<WorkOrderStatusHistoryEntry>(entry => entry.ToStatus == WorkOrderStatus.Completed),
            Arg.Any<CancellationToken>());
        await fixture.Audit.Received(1).AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_WhenPersistenceFails_RollsBackAndDoesNotCommit()
    {
        var fixture = new WorkOrderFixture();
        fixture.SpareParts.UpdateAsync(Arg.Any<SparePart>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("simulated")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CompleteAsync(10, 90));

        await fixture.UnitOfWork.Received(1).RollbackAsync(CancellationToken.None);
        await fixture.UnitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await fixture.WorkOrders.DidNotReceive().UpdateAsync(Arg.Any<WorkOrder>(), Arg.Any<CancellationToken>());
        await fixture.Audit.DidNotReceive().AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_WhenAuditFails_RollsBackWholeUseCase()
    {
        var fixture = new WorkOrderFixture();
        fixture.Audit.AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("audit failed")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CompleteAsync(10, 90));

        await fixture.UnitOfWork.Received(1).RollbackAsync(CancellationToken.None);
        await fixture.UnitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    private sealed class WorkOrderFixture
    {
        public WorkOrderFixture()
        {
            UnitOfWork = Substitute.For<IUnitOfWork>();
            WorkOrders = Substitute.For<IWorkOrderRepository>();
            SpareParts = Substitute.For<ISparePartRepository>();
            StockMovements = Substitute.For<IStockMovementRepository>();
            EquipmentRepository = Substitute.For<IEquipmentRepository>();
            Audit = Substitute.For<IAuditRepository>();

            Order = CorrectiveWorkOrder.Rehydrate(
                10, "WO-TEST-10", 20, 30, Priority.High, "Test işi", null,
                WorkOrderStatus.InProgress, 7, Now.AddHours(-1), null, null, 0,
                Now.AddHours(-2), Now.AddHours(-1), [1]);
            Order.AddRehydratedPart(WorkOrderPart.Rehydrate(
                100, 1, 2m, 25m, "TRY", Now.AddHours(-1), Now.AddHours(-1), [1]));
            Part = SparePart.Rehydrate(
                1, null, "P-1", "Rulman", 5m, 1m, 25m, "TRY",
                Now.AddDays(-1), Now.AddDays(-1), [1]);
            Equipment = MaintainIQ.Domain.Entities.Equipment.Rehydrate(
                20, null, "EQ-20", "Pompa", "Hat 1", "qr", EquipmentStatus.Faulty,
                false, Now.AddDays(-1), Now.AddDays(-1), [1]);

            WorkOrders.GetWithPartsAsync(10, Arg.Any<CancellationToken>()).Returns(Order);
            SpareParts.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(Part);
            EquipmentRepository.GetByIdAsync(20, Arg.Any<CancellationToken>()).Returns(Equipment);
            StockMovements.AddAsync(Arg.Any<StockMovement>(), Arg.Any<CancellationToken>()).Returns(1);
            Audit.AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>()).Returns(1);

            var session = Substitute.For<IServiceDataSession>();
            session.UnitOfWork.Returns(UnitOfWork);
            session.WorkOrders.Returns(WorkOrders);
            session.SpareParts.Returns(SpareParts);
            session.StockMovements.Returns(StockMovements);
            session.Equipment.Returns(EquipmentRepository);
            session.Audit.Returns(Audit);
            var factory = Substitute.For<IServiceDataSessionFactory>();
            factory.Create().Returns(session);

            var currentUser = new CurrentUserContext();
            currentUser.SignIn(1, 7, "admin", UserRole.Admin);
            Service = new WorkOrderService(
                factory,
                currentUser,
                new PermissionService(currentUser),
                new FixedClock(Now));
        }

        public IUnitOfWork UnitOfWork { get; }
        public IWorkOrderRepository WorkOrders { get; }
        public ISparePartRepository SpareParts { get; }
        public IStockMovementRepository StockMovements { get; }
        public IEquipmentRepository EquipmentRepository { get; }
        public IAuditRepository Audit { get; }
        public CorrectiveWorkOrder Order { get; }
        public SparePart Part { get; }
        public MaintainIQ.Domain.Entities.Equipment Equipment { get; }
        public WorkOrderService Service { get; }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
