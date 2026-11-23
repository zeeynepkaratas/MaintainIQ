using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Services.Security;

public enum Permission
{
    ManageEquipment,
    ReportFailure,
    ManageFailure,
    CreateWorkOrderFromFailure,
    AssignWorkOrder,
    ExecuteOwnWorkOrder,
    CloseReopenCancelWorkOrder,
    ManageInventory,
    ManageMaintenancePlan,
    ViewAnalytics,
    ManageAdvisor,
    ManageUsers,
    ViewAudit,
}

public interface IPermissionService
{
    bool HasPermission(Permission permission);
    void Ensure(Permission permission);
}

public sealed class PermissionService(ICurrentUser currentUser) : IPermissionService
{
    private static readonly IReadOnlyDictionary<UserRole, IReadOnlySet<Permission>> Matrix =
        new Dictionary<UserRole, IReadOnlySet<Permission>>
        {
            [UserRole.Admin] = Enum.GetValues<Permission>().ToHashSet(),
            [UserRole.Supervisor] = new HashSet<Permission>
            {
                Permission.ManageEquipment,
                Permission.ReportFailure,
                Permission.ManageFailure,
                Permission.CreateWorkOrderFromFailure,
                Permission.AssignWorkOrder,
                Permission.ExecuteOwnWorkOrder,
                Permission.CloseReopenCancelWorkOrder,
                Permission.ManageInventory,
                Permission.ManageMaintenancePlan,
                Permission.ViewAnalytics,
                Permission.ManageAdvisor,
                Permission.ViewAudit,
            },
            [UserRole.Technician] = new HashSet<Permission>
            {
                Permission.ReportFailure,
                Permission.ExecuteOwnWorkOrder,
                Permission.ViewAnalytics,
            },
            [UserRole.Operator] = new HashSet<Permission>
            {
                Permission.ReportFailure,
                Permission.ViewAnalytics,
            },
        };

    public bool HasPermission(Permission permission) =>
        currentUser.IsAuthenticated
        && Matrix.TryGetValue(currentUser.Role, out var permissions)
        && permissions.Contains(permission);

    public void Ensure(Permission permission)
    {
        if (!HasPermission(permission))
        {
            throw new PermissionDeniedException($"{permission} işlemi için yetkiniz bulunmuyor.");
        }
    }
}
