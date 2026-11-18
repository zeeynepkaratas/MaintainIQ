using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Tests.Domain;

public sealed class SupportingEntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Technician_ExposesIdentityAndActivationState()
    {
        var technician = new Technician("T-1", "Zeynep", "Karataş", Now, "Mekanik");

        technician.Deactivate();
        Assert.False(technician.IsActive);
        technician.Activate();

        Assert.True(technician.IsActive);
        Assert.Equal("Zeynep Karataş", technician.FullName);
        Assert.Equal("Mekanik", technician.Specialty);
    }

    [Fact]
    public void PersonSubtypes_CanBeConstructed()
    {
        Person[] people =
        [
            new Supervisor("S-1", "Ayşe", "Yılmaz", Now),
            new AdminPerson("A-1", "Ali", "Demir", Now),
            new OperatorPerson("O-1", "Can", "Kaya", Now),
        ];

        Assert.All(people, person => Assert.True(person.IsActive));
    }

    [Fact]
    public void FailureCause_CanBeDeactivated()
    {
        var cause = new FailureCause("bearing", "Rulman", Now);

        cause.Deactivate();

        Assert.Equal("BEARING", cause.Code);
        Assert.False(cause.IsActive);
    }

    [Fact]
    public void Supplier_NormalizesCodeAndStoresContact()
    {
        var supplier = new Supplier(" sup-1 ", "Tedarikçi", Now, "mail@example.com");

        Assert.Equal("SUP-1", supplier.Code);
        Assert.Equal("mail@example.com", supplier.Contact);
        Assert.True(supplier.IsActive);
    }

    [Fact]
    public void StockMovement_StoresImmutableSnapshot()
    {
        var movement = new StockMovement(
            1,
            StockMovementType.Consumption,
            2,
            8,
            new Money(50),
            Now,
            "İş emrinde kullanıldı",
            10);

        Assert.Equal(8, movement.ResultingQuantity);
        Assert.Equal(10, movement.WorkOrderId);
        Assert.Equal(new Money(50), movement.UnitCost);
    }

    [Fact]
    public void StockMovement_RejectsInvalidPart()
    {
        Assert.Throws<DomainValidationException>(() =>
            new StockMovement(0, StockMovementType.Receipt, 1, 1, new Money(5), Now, "Giriş"));
    }

    [Fact]
    public void Attachment_StoresSafeFileMetadata()
    {
        var attachment = new Attachment(
            AttachmentOwnerType.Failure,
            5,
            "safe-name.jpg",
            "arıza.jpg",
            "image/jpeg",
            1024,
            Now);

        Assert.Equal(AttachmentOwnerType.Failure, attachment.OwnerType);
        Assert.Equal("safe-name.jpg", attachment.StoredFileName);
        Assert.Equal(1024, attachment.SizeBytes);
    }

    [Fact]
    public void Attachment_RejectsEmptyFile()
    {
        Assert.Throws<DomainValidationException>(() =>
            new Attachment(AttachmentOwnerType.Equipment, 1, "a.jpg", "a.jpg", "image/jpeg", 0, Now));
    }

    [Fact]
    public void AuditLog_StoresOldAndNewValues()
    {
        var audit = new AuditLog("Equipment", "15", "Update", 2, Now, "{\"name\":\"A\"}", "{\"name\":\"B\"}");

        Assert.Equal("Equipment", audit.EntityName);
        Assert.Equal("15", audit.EntityId);
        Assert.NotNull(audit.OldValuesJson);
        Assert.NotNull(audit.NewValuesJson);
    }

    [Fact]
    public void AuditLog_RequiresActor()
    {
        Assert.Throws<DomainValidationException>(() =>
            new AuditLog("Equipment", "1", "Update", 0, Now, null, null));
    }

    [Fact]
    public void WorkOrderComment_CanBeAdded()
    {
        var order = new CorrectiveWorkOrder("WO-COMMENT", 1, 1, Priority.Normal, "Test", Now);

        order.AddComment(new WorkOrderComment(3, "Kontrol edildi", Now.AddMinutes(1)));

        Assert.Single(order.Comments);
        Assert.Equal("Kontrol edildi", order.Comments.Single().Text);
    }

    [Fact]
    public void WorkOrderComment_RequiresAuthor()
    {
        Assert.Throws<DomainValidationException>(() => new WorkOrderComment(0, "Not", Now));
    }

    [Fact]
    public void BaseEntity_RejectsUpdateBeforeCreation()
    {
        var equipment = new Equipment("EQ-1", "Pompa", "Hat A", Now);

        Assert.Throws<DomainValidationException>(() => equipment.MarkUpdated(Now.AddSeconds(-1)));
    }

    [Theory]
    [InlineData(MaintenancePeriodUnit.Day, 2, 2026, 11, 20)]
    [InlineData(MaintenancePeriodUnit.Week, 2, 2026, 12, 2)]
    [InlineData(MaintenancePeriodUnit.Year, 1, 2027, 11, 18)]
    public void MaintenancePlan_CalculatesPeriodUnits(
        MaintenancePeriodUnit unit,
        int value,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var plan = new MaintenancePlan(1, "Plan", value, unit, Priority.Normal, 30, new DateOnly(2026, 11, 18), Now);

        var next = plan.CalculateNextDue(new DateOnly(2026, 11, 18));

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), next);
    }

    [Fact]
    public void MaintenancePlan_CanBeDeactivatedAndActivated()
    {
        var plan = new MaintenancePlan(1, "Plan", 1, MaintenancePeriodUnit.Month, Priority.Normal, 30, new DateOnly(2026, 12, 1), Now);

        plan.Deactivate();
        Assert.False(plan.IsActive);
        plan.Activate();

        Assert.True(plan.IsActive);
    }

    [Fact]
    public void UserAccount_CanChangeRoleAndActivation()
    {
        var account = new UserAccount(1, "user", UserRole.Operator, "hash", "salt", Now);

        account.ChangeRole(UserRole.Supervisor);
        account.Deactivate();
        Assert.False(account.IsActive);
        account.Activate();

        Assert.Equal(UserRole.Supervisor, account.Role);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void PreventiveWorkOrder_RejectsInvalidPlan()
    {
        Assert.Throws<DomainValidationException>(() =>
            new PreventiveWorkOrder("PM-1", 1, 0, new DateOnly(2026, 12, 1), Priority.Normal, "Bakım", Now));
    }

    [Fact]
    public void WorkOrderPart_RejectsInvalidQuantity()
    {
        Assert.Throws<DomainValidationException>(() => new WorkOrderPart(1, 0, new Money(10), Now));
    }
}
