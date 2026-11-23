using MaintainIQ.Data.ReadModels;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Data.Repositories;

public interface IEquipmentRepository : IRepository<Equipment>
{
    Task<IReadOnlyList<EquipmentTreeItemDto>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EquipmentTreeItemDto>> GetDescendantsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EquipmentTreeItemDto>> GetPathAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> SearchAsync(string filter, CancellationToken cancellationToken = default);
    Task<bool> ExistsOpenWorkAsync(int id, CancellationToken cancellationToken = default);
}

public interface IWorkOrderRepository : IRepository<WorkOrder>
{
    Task<IReadOnlyList<WorkOrderListItemDto>> SearchAsync(WorkOrderFilter filter, CancellationToken cancellationToken = default);
    Task<WorkOrder?> GetWithPartsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrder>> GetOpenForEquipmentAsync(int equipmentId, CancellationToken cancellationToken = default);
    Task AddPartAsync(int workOrderId, WorkOrderPart part, CancellationToken cancellationToken = default);
    Task AddStatusHistoryAsync(int workOrderId, WorkOrderStatusHistoryEntry entry, CancellationToken cancellationToken = default);
    Task<bool> HasScheduleConflictAsync(
        int technicianId,
        DateRange plannedWindow,
        int? excludedWorkOrderId = null,
        CancellationToken cancellationToken = default);
}

public interface IFailureRepository : IRepository<Failure>;

public interface ISparePartRepository : IRepository<SparePart>
{
    Task<SparePart?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparePart>> GetBelowMinimumAsync(CancellationToken cancellationToken = default);
}

public interface IStockMovementRepository : IRepository<StockMovement>;

public interface IMaintenancePlanRepository : IRepository<MaintenancePlan>
{
    Task<IReadOnlyList<MaintenancePlan>> GetDueAsync(DateOnly date, CancellationToken cancellationToken = default);
}

public interface ITechnicianRepository : IRepository<Technician>;

public interface IUserRepository : IRepository<UserAccount>
{
    Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
}

public interface IAttachmentRepository : IRepository<Attachment>
{
    Task<IReadOnlyList<Attachment>> GetForOwnerAsync(byte ownerType, int ownerId, CancellationToken cancellationToken = default);
}

public interface IAuditRepository : IRepository<AuditLog>;

public interface IReportQueries
{
    Task<MaintenanceKpiDto> GetMaintenanceKpisAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrderListItemDto>> GetWorkOrderReportAsync(WorkOrderFilter filter, CancellationToken cancellationToken = default);
}
