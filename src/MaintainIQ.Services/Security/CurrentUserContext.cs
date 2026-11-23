using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Services.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int UserAccountId { get; }
    int PersonId { get; }
    string Username { get; }
    UserRole Role { get; }
}

public interface ICurrentUserContext : ICurrentUser
{
    void SignIn(int userAccountId, int personId, string username, UserRole role);
    void SignOut();
}

public sealed class CurrentUserContext : ICurrentUserContext
{
    public bool IsAuthenticated { get; private set; }
    public int UserAccountId { get; private set; }
    public int PersonId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }

    public void SignIn(int userAccountId, int personId, string username, UserRole role)
    {
        if (userAccountId <= 0 || personId <= 0 || string.IsNullOrWhiteSpace(username))
        {
            throw new DomainValidationException("Oturum bilgileri geçersizdir.");
        }

        UserAccountId = userAccountId;
        PersonId = personId;
        Username = username;
        Role = role;
        IsAuthenticated = true;
    }

    public void SignOut()
    {
        IsAuthenticated = false;
        UserAccountId = 0;
        PersonId = 0;
        Username = string.Empty;
        Role = default;
    }
}
