using MaintainIQ.Data.Repositories;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Authentication;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using NSubstitute;

namespace MaintainIQ.Tests.Services;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoginAsync_WithValidPassword_OpensSessionAndResetsFailures()
    {
        var fixture = new AuthFixture();
        var account = fixture.CreateAccount("GucluParola!42", failedLoginCount: 2);
        fixture.Users.GetByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(account);

        var result = await fixture.Service.LoginAsync("admin", "GucluParola!42");

        Assert.True(fixture.CurrentUser.IsAuthenticated);
        Assert.Equal(account.Id, result.UserAccountId);
        Assert.Equal(0, account.FailedLoginCount);
        await fixture.Users.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_FiveWrongPasswords_LocksAccountForTenMinutes()
    {
        var fixture = new AuthFixture();
        var account = fixture.CreateAccount("GucluParola!42");
        fixture.Users.GetByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(account);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<AuthenticationException>(() =>
                fixture.Service.LoginAsync("admin", "yanlis-parola"));
        }

        Assert.Equal(Now.AddMinutes(10), account.LockedUntil);
        Assert.Equal(0, account.FailedLoginCount);
        await fixture.Users.Received(5).UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    private sealed class AuthFixture
    {
        private readonly PasswordHasher _hasher = new();

        public AuthFixture()
        {
            UnitOfWork = Substitute.For<IUnitOfWork>();
            Users = Substitute.For<IUserRepository>();
            var audit = Substitute.For<IAuditRepository>();
            audit.AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>()).Returns(1);
            var session = Substitute.For<IServiceDataSession>();
            session.UnitOfWork.Returns(UnitOfWork);
            session.Users.Returns(Users);
            session.Audit.Returns(audit);
            var factory = Substitute.For<IServiceDataSessionFactory>();
            factory.Create().Returns(session);
            CurrentUser = new CurrentUserContext();
            Service = new AuthService(factory, CurrentUser, _hasher, new FixedClock(Now));
        }

        public IUnitOfWork UnitOfWork { get; }
        public IUserRepository Users { get; }
        public CurrentUserContext CurrentUser { get; }
        public AuthService Service { get; }

        public UserAccount CreateAccount(string password, int failedLoginCount = 0)
        {
            var credential = _hasher.Hash(password);
            return UserAccount.Rehydrate(
                11, 7, "admin", UserRole.Admin, credential.Hash, credential.Salt,
                true, failedLoginCount, null, Now.AddDays(-1), Now.AddDays(-1), [1]);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
