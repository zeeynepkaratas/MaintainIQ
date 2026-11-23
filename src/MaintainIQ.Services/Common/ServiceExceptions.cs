using MaintainIQ.Domain.Common;

namespace MaintainIQ.Services.Common;

public sealed class AuthenticationException : DomainException
{
    public AuthenticationException(string message)
        : base(message)
    {
    }
}

public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, int id)
        : base($"{entityName} kaydı bulunamadı. Kimlik: {id}.")
    {
    }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
