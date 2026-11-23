using MaintainIQ.Data.Repositories;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using NSubstitute;

namespace MaintainIQ.Tests.Services;

internal sealed class ServiceFixture
{
    public static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    public ServiceFixture()
    {
        UnitOfWork = Substitute.For<IUnitOfWork>();
        Equipment = Substitute.For<IEquipmentRepository>();
        Failures = Substitute.For<IFailureRepository>();
        WorkOrders = Substitute.For<IWorkOrderRepository>();
        SpareParts = Substitute.For<ISparePartRepository>();
        StockMovements = Substitute.For<IStockMovementRepository>();
        Technicians = Substitute.For<ITechnicianRepository>();
        Users = Substitute.For<IUserRepository>();
        Audit = Substitute.For<IAuditRepository>();

        Session = Substitute.For<IServiceDataSession>();
        Session.UnitOfWork.Returns(UnitOfWork);
        Session.Equipment.Returns(Equipment);
        Session.Failures.Returns(Failures);
        Session.WorkOrders.Returns(WorkOrders);
        Session.SpareParts.Returns(SpareParts);
        Session.StockMovements.Returns(StockMovements);
        Session.Technicians.Returns(Technicians);
        Session.Users.Returns(Users);
        Session.Audit.Returns(Audit);

        Factory = Substitute.For<IServiceDataSessionFactory>();
        Factory.Create().Returns(Session);
        CurrentUser = new CurrentUserContext();
        CurrentUser.SignIn(1, 7, "admin", MaintainIQ.Domain.Enums.UserRole.Admin);
        Permissions = new PermissionService(CurrentUser);
        Clock = new FixedClock(Now);

        Equipment.AddAsync(Arg.Any<MaintainIQ.Domain.Entities.Equipment>(), Arg.Any<CancellationToken>()).Returns(101);
        Failures.AddAsync(Arg.Any<MaintainIQ.Domain.Entities.Failure>(), Arg.Any<CancellationToken>()).Returns(102);
        WorkOrders.AddAsync(Arg.Any<MaintainIQ.Domain.Entities.WorkOrder>(), Arg.Any<CancellationToken>()).Returns(103);
        SpareParts.AddAsync(Arg.Any<MaintainIQ.Domain.Entities.SparePart>(), Arg.Any<CancellationToken>()).Returns(104);
        StockMovements.AddAsync(Arg.Any<MaintainIQ.Domain.Entities.StockMovement>(), Arg.Any<CancellationToken>()).Returns(105);
        Technicians.AddAsync(Arg.Any<MaintainIQ.Domain.Entities.Technician>(), Arg.Any<CancellationToken>()).Returns(106);
        Audit.AddAsync(Arg.Any<MaintainIQ.Domain.Entities.AuditLog>(), Arg.Any<CancellationToken>()).Returns(107);
    }

    public IUnitOfWork UnitOfWork { get; }
    public IEquipmentRepository Equipment { get; }
    public IFailureRepository Failures { get; }
    public IWorkOrderRepository WorkOrders { get; }
    public ISparePartRepository SpareParts { get; }
    public IStockMovementRepository StockMovements { get; }
    public ITechnicianRepository Technicians { get; }
    public IUserRepository Users { get; }
    public IAuditRepository Audit { get; }
    public IServiceDataSession Session { get; }
    public IServiceDataSessionFactory Factory { get; }
    public CurrentUserContext CurrentUser { get; }
    public PermissionService Permissions { get; }
    public IClock Clock { get; }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
