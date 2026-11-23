using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Tests.Services;

public sealed class SecurityTests
{
    [Fact]
    public void PasswordHasher_UsesRandomSaltAndFixedTimeCompatibleHashes()
    {
        var hasher = new PasswordHasher();

        var first = hasher.Hash("GucluParola!42");
        var second = hasher.Hash("GucluParola!42");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
        Assert.True(hasher.Verify("GucluParola!42", first.Hash, first.Salt));
        Assert.False(hasher.Verify("YanlisParola", first.Hash, first.Salt));
        Assert.True(PasswordHasher.Iterations >= 100_000);
    }

    [Fact]
    public void PasswordHasher_RejectsShortPassword()
    {
        var hasher = new PasswordHasher();

        Assert.Throws<DomainValidationException>(() => hasher.Hash("kisa"));
    }

    [Theory]
    [InlineData(UserRole.Admin, Permission.ManageUsers, true)]
    [InlineData(UserRole.Supervisor, Permission.ManageUsers, false)]
    [InlineData(UserRole.Supervisor, Permission.ViewAudit, true)]
    [InlineData(UserRole.Technician, Permission.ExecuteOwnWorkOrder, true)]
    [InlineData(UserRole.Technician, Permission.ManageInventory, false)]
    [InlineData(UserRole.Operator, Permission.ReportFailure, true)]
    [InlineData(UserRole.Operator, Permission.AssignWorkOrder, false)]
    public void PermissionMatrix_EnforcesDocumentedRoles(
        UserRole role,
        Permission permission,
        bool expected)
    {
        var currentUser = new CurrentUserContext();
        currentUser.SignIn(1, 2, "test", role);
        var service = new PermissionService(currentUser);

        Assert.Equal(expected, service.HasPermission(permission));
        if (expected)
        {
            service.Ensure(permission);
        }
        else
        {
            Assert.Throws<PermissionDeniedException>(() => service.Ensure(permission));
        }
    }

    [Fact]
    public void PermissionMatrix_RejectsUnauthenticatedUser()
    {
        var service = new PermissionService(new CurrentUserContext());

        Assert.False(service.HasPermission(Permission.ReportFailure));
        Assert.Throws<PermissionDeniedException>(() => service.Ensure(Permission.ReportFailure));
    }
}
