using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Domain.Entities;

public sealed class UserAccount : BaseEntity
{
    public UserAccount(
        int personId,
        string username,
        UserRole role,
        string passwordHash,
        string passwordSalt,
        DateTimeOffset createdAt)
        : base(createdAt)
    {
        if (personId <= 0)
        {
            throw new DomainValidationException("Geçerli bir personel seçilmelidir.");
        }

        PersonId = personId;
        Username = Guard.Required(username, "Kullanıcı adı", 80).ToLowerInvariant();
        Role = role;
        PasswordHash = Guard.Required(passwordHash, "Parola özeti", 512);
        PasswordSalt = Guard.Required(passwordSalt, "Parola tuzu", 256);
    }

    public int PersonId { get; private set; }

    public string Username { get; private set; }

    public UserRole Role { get; private set; }

    public string PasswordHash { get; private set; }

    public string PasswordSalt { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    internal static UserAccount Rehydrate(
        int id, int personId, string username, UserRole role, string passwordHash,
        string passwordSalt, bool isActive, int failedLoginCount, DateTimeOffset? lockedUntil,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, byte[] rowVersion)
    {
        var account = new UserAccount(personId, username, role, passwordHash, passwordSalt, createdAt)
        {
            IsActive = isActive,
            FailedLoginCount = failedLoginCount,
            LockedUntil = lockedUntil,
        };
        account.RestorePersistenceState(id, createdAt, updatedAt, rowVersion);
        return account;
    }

    public bool IsLocked(DateTimeOffset now) => LockedUntil > now;

    public void RecordFailedLogin(DateTimeOffset now)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= 5)
        {
            LockedUntil = now.AddMinutes(10);
            FailedLoginCount = 0;
        }
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public void ChangePassword(string passwordHash, string passwordSalt, DateTimeOffset changedAt)
    {
        PasswordHash = Guard.Required(passwordHash, "Parola özeti", 512);
        PasswordSalt = Guard.Required(passwordSalt, "Parola tuzu", 256);
        MarkUpdated(changedAt);
    }

    public void ChangeRole(UserRole role) => Role = role;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
