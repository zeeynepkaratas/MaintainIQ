using MaintainIQ.Data.Connection;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Data.Transactions;

namespace MaintainIQ.Services.Persistence;

public interface IServiceDataSession : IAsyncDisposable
{
    IUnitOfWork UnitOfWork { get; }
    IEquipmentRepository Equipment { get; }
    IFailureRepository Failures { get; }
    IWorkOrderRepository WorkOrders { get; }
    ISparePartRepository SpareParts { get; }
    IStockMovementRepository StockMovements { get; }
    ITechnicianRepository Technicians { get; }
    IUserRepository Users { get; }
    IAuditRepository Audit { get; }
    IMaintenancePlanRepository MaintenancePlans { get; }
    IAttachmentRepository Attachments { get; }
}

public interface IServiceDataSessionFactory
{
    IServiceDataSession Create();
}

public sealed class ServiceDataSessionFactory(IDbConnectionFactory connectionFactory)
    : IServiceDataSessionFactory
{
    public IServiceDataSession Create() => new ServiceDataSession(new UnitOfWork(connectionFactory));
}

public sealed class ServiceDataSession : IServiceDataSession
{
    public ServiceDataSession(IUnitOfWork unitOfWork)
    {
        UnitOfWork = unitOfWork;
        Equipment = new EquipmentRepository(unitOfWork);
        Failures = new FailureRepository(unitOfWork);
        WorkOrders = new WorkOrderRepository(unitOfWork);
        SpareParts = new SparePartRepository(unitOfWork);
        StockMovements = new StockMovementRepository(unitOfWork);
        Technicians = new TechnicianRepository(unitOfWork);
        Users = new UserRepository(unitOfWork);
        Audit = new AuditRepository(unitOfWork);
        MaintenancePlans = new MaintenancePlanRepository(unitOfWork);
        Attachments = new AttachmentRepository(unitOfWork);
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
    public IMaintenancePlanRepository MaintenancePlans { get; }
    public IAttachmentRepository Attachments { get; }

    public ValueTask DisposeAsync() => UnitOfWork.DisposeAsync();
}
