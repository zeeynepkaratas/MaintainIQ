using System.Text.Json;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Services.Auditing;

internal sealed class AuditWriter(ICurrentUser currentUser)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task WriteAsync(
        IServiceDataSession session,
        string entityName,
        string entityId,
        string action,
        object? oldValues,
        object? newValues,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLog(
            entityName,
            entityId,
            action,
            currentUser.PersonId,
            at,
            Serialize(oldValues),
            Serialize(newValues));
        return session.Audit.AddAsync(entry, cancellationToken);
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}
