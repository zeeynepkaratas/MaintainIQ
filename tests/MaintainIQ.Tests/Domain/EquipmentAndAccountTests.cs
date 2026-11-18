using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Tests.Domain;

public sealed class EquipmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_NormalizesCodeAndStartsOperational()
    {
        var equipment = new Equipment(" pom-01 ", "Pompa", "Hat A", Now);

        Assert.Equal("POM-01", equipment.Code);
        Assert.Equal(EquipmentStatus.Operational, equipment.Status);
    }

    [Fact]
    public void MoveUnder_BuildsHierarchy()
    {
        var parent = Create("PARENT");
        var child = Create("CHILD");

        child.MoveUnder(parent);

        Assert.Same(parent, child.Parent);
        Assert.Contains(child, parent.Children);
    }

    [Fact]
    public void MoveUnder_RejectsDescendantCycle()
    {
        var parent = Create("PARENT");
        var child = Create("CHILD");
        child.MoveUnder(parent);

        var error = Assert.Throws<BusinessRuleException>(() => parent.MoveUnder(child));

        Assert.Equal("BR-07", error.RuleCode);
    }

    [Fact]
    public void SoftDelete_RejectsActiveChild()
    {
        var parent = Create("PARENT");
        Create("CHILD").MoveUnder(parent);

        var error = Assert.Throws<BusinessRuleException>(() => parent.SoftDelete(false));

        Assert.Equal("BR-08", error.RuleCode);
    }

    [Fact]
    public void SoftDelete_DeletesLeafWithoutOpenWork()
    {
        var equipment = Create("LEAF");

        equipment.SoftDelete(false);

        Assert.True(equipment.IsDeleted);
    }

    private static Equipment Create(string code) => new(code, code, "Hat A", Now);
}

public sealed class UserAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_NormalizesUsername()
    {
        var account = Create(" ZEYNEP ");

        Assert.Equal("zeynep", account.Username);
    }

    [Fact]
    public void FifthFailure_LocksAccountForTenMinutes()
    {
        var account = Create("zeynep");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            account.RecordFailedLogin(Now);
        }

        Assert.True(account.IsLocked(Now.AddMinutes(9)));
        Assert.False(account.IsLocked(Now.AddMinutes(10)));
    }

    [Fact]
    public void SuccessfulLogin_ClearsFailures()
    {
        var account = Create("zeynep");
        account.RecordFailedLogin(Now);

        account.RecordSuccessfulLogin();

        Assert.Equal(0, account.FailedLoginCount);
        Assert.Null(account.LockedUntil);
    }

    private static UserAccount Create(string username) =>
        new(1, username, UserRole.Admin, "hash", "salt", Now);
}
