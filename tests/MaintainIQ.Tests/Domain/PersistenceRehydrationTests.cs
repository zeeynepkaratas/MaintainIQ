using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Tests.Domain;

public sealed class PersistenceRehydrationTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAt = CreatedAt.AddHours(2);
    private static readonly byte[] RowVersion = [1, 2, 3, 4];

    [Fact]
    public void Rehydrate_restores_equipment_failure_and_plan_state()
    {
        var equipment = Equipment.Rehydrate(
            10, 4, "EQ-10", "Pompa", "Hat 1", "qr-10",
            EquipmentStatus.UnderMaintenance, true, CreatedAt, UpdatedAt, RowVersion);
        var failure = Failure.Rehydrate(
            11, 10, 2, FailureSeverity.High, FailureStatus.Resolved,
            "Rulman arızası", 3, CreatedAt, UpdatedAt, CreatedAt, UpdatedAt, RowVersion);
        var plan = MaintenancePlan.Rehydrate(
            12, 10, "Aylık bakım", 1, MaintenancePeriodUnit.Month,
            Priority.High, 90, new DateOnly(2026, 12, 1), false,
            CreatedAt, UpdatedAt, RowVersion);

        Assert.Equal(10, equipment.Id);
        Assert.Equal(4, equipment.ParentId);
        Assert.True(equipment.IsDeleted);
        Assert.Equal(FailureStatus.Resolved, failure.Status);
        Assert.Equal(UpdatedAt, failure.ResolvedAt);
        Assert.False(plan.IsActive);
        Assert.Equal(RowVersion, plan.RowVersion);
    }

    [Fact]
    public void Rehydrate_restores_inventory_person_account_and_supporting_state()
    {
        var part = SparePart.Rehydrate(
            20, 7, "SP-20", "Filtre", 12, 3, 45, "TRY",
            CreatedAt, UpdatedAt, RowVersion);
        var movement = StockMovement.Rehydrate(
            21, 20, 30, StockMovementType.Consumption, 2, 10, 45, "TRY",
            "İş emri tüketimi", CreatedAt, UpdatedAt, RowVersion);
        var technician = Technician.Rehydrate(
            22, "T-22", "Ada", "Yılmaz", "Elektrik", false,
            CreatedAt, UpdatedAt, RowVersion);
        var account = UserAccount.Rehydrate(
            23, 22, "ADA", UserRole.Technician, "hash", "salt", false, 3,
            UpdatedAt.AddMinutes(5), CreatedAt, UpdatedAt, RowVersion);
        var attachment = Attachment.Rehydrate(
            24, AttachmentOwnerType.Equipment, 10, "stored.pdf", "manual.pdf",
            "application/pdf", 2048, CreatedAt, UpdatedAt, RowVersion);
        var audit = AuditLog.Rehydrate(
            25, "Equipment", "10", "Update", 22, UpdatedAt,
            "{\"status\":1}", "{\"status\":3}", CreatedAt, UpdatedAt, RowVersion);

        Assert.Equal(7, part.SupplierId);
        Assert.Equal(10, movement.ResultingQuantity);
        Assert.False(technician.IsActive);
        Assert.Equal(3, account.FailedLoginCount);
        Assert.Equal("manual.pdf", attachment.OriginalFileName);
        Assert.Equal("Update", audit.Action);
    }

    [Fact]
    public void Rehydrate_restores_work_orders_and_parts()
    {
        var corrective = CorrectiveWorkOrder.Rehydrate(
            30, "WO-30", 10, 11, Priority.Critical, "Arıza onarımı", null,
            WorkOrderStatus.Completed, 22, CreatedAt.AddMinutes(10), UpdatedAt,
            null, 75, CreatedAt, UpdatedAt, RowVersion);
        var preventive = PreventiveWorkOrder.Rehydrate(
            31, "WO-31", 10, 12, new DateOnly(2026, 12, 1), Priority.Normal,
            "Planlı bakım", null, WorkOrderStatus.Assigned, 22, null, null,
            null, 0, CreatedAt, UpdatedAt, RowVersion);
        var part = WorkOrderPart.Rehydrate(
            32, 20, 2, 45, "TRY", CreatedAt, UpdatedAt, RowVersion);

        Assert.Equal(11, corrective.FailureId);
        Assert.Equal(75, corrective.LaborMinutes);
        Assert.Equal(12, preventive.MaintenancePlanId);
        Assert.Equal(22, preventive.AssignedTechnicianId);
        Assert.Equal(90, part.TotalCost.Amount);
    }
}
