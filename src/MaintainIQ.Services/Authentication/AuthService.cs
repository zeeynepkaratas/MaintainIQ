using MaintainIQ.Domain.Entities;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Services.Authentication;

public sealed record AuthenticatedUser(int UserAccountId, int PersonId, string Username, Domain.Enums.UserRole Role);

public interface IAuthService
{
    Task<AuthenticatedUser> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    void Logout();
}

public sealed class AuthService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUserContext currentUser,
    IPasswordHasher passwordHasher,
    IClock clock) : IAuthService
{
    public async Task<AuthenticatedUser> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var account = await session.Users.GetByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
            var now = clock.UtcNow;
            if (account is null || !account.IsActive)
            {
                throw new AuthenticationException("Kullanıcı adı veya parola hatalı.");
            }

            if (account.IsLocked(now))
            {
                throw new AuthenticationException($"Hesap {account.LockedUntil:HH:mm} saatine kadar kilitli.");
            }

            if (!passwordHasher.Verify(password, account.PasswordHash, account.PasswordSalt))
            {
                account.RecordFailedLogin(now);
                account.MarkUpdated(now);
                await session.Users.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
                await WriteLoginAuditAsync(session, account, "LoginFailed", now, cancellationToken).ConfigureAwait(false);
                await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                throw new AuthenticationException("Kullanıcı adı veya parola hatalı.");
            }

            account.RecordSuccessfulLogin();
            account.MarkUpdated(now);
            await session.Users.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
            await WriteLoginAuditAsync(session, account, "LoginSucceeded", now, cancellationToken).ConfigureAwait(false);
            await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            currentUser.SignIn(account.Id, account.PersonId, account.Username, account.Role);
            return new(account.Id, account.PersonId, account.Username, account.Role);
        }
        catch
        {
            await session.UnitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new AuthenticationException("Parola değiştirmek için oturum açılmalıdır.");
        }

        await using var session = sessionFactory.Create();
        await session.UnitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var account = await session.Users.GetByIdAsync(currentUser.UserAccountId, cancellationToken).ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(UserAccount), currentUser.UserAccountId);
            if (!passwordHasher.Verify(currentPassword, account.PasswordHash, account.PasswordSalt))
            {
                throw new AuthenticationException("Mevcut parola hatalı.");
            }

            var now = clock.UtcNow;
            var credential = passwordHasher.Hash(newPassword);
            account.ChangePassword(credential.Hash, credential.Salt, now);
            await session.Users.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
            var audit = new AuditWriter(currentUser);
            await audit.WriteAsync(
                session, nameof(UserAccount), account.Id.ToString(), "PasswordChanged",
                null, new { account.Username }, now, cancellationToken).ConfigureAwait(false);
            await session.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await session.UnitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public void Logout() => currentUser.SignOut();

    private static Task WriteLoginAuditAsync(
        IServiceDataSession session,
        UserAccount account,
        string action,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLog(
            nameof(UserAccount), account.Id.ToString(), action, account.PersonId, now,
            null, System.Text.Json.JsonSerializer.Serialize(new { account.Username }));
        return session.Audit.AddAsync(entry, cancellationToken);
    }
}
