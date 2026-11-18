using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Domain.Entities;

public sealed class Attachment : BaseEntity
{
    public Attachment(
        AttachmentOwnerType ownerType,
        int ownerId,
        string storedFileName,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset createdAt)
        : base(createdAt)
    {
        if (ownerId <= 0 || sizeBytes <= 0)
        {
            throw new DomainValidationException("Ek sahibi ve dosya boyutu geçerli olmalıdır.");
        }

        OwnerType = ownerType;
        OwnerId = ownerId;
        StoredFileName = Guard.Required(storedFileName, "Saklanan dosya adı", 260);
        OriginalFileName = Guard.Required(originalFileName, "Özgün dosya adı", 260);
        ContentType = Guard.Required(contentType, "İçerik türü", 100);
        SizeBytes = sizeBytes;
    }

    public AttachmentOwnerType OwnerType { get; private set; }
    public int OwnerId { get; private set; }
    public string StoredFileName { get; private set; }
    public string OriginalFileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
}

public sealed class AuditLog : BaseEntity
{
    public AuditLog(
        string entityName,
        string entityId,
        string action,
        int performedByPersonId,
        DateTimeOffset performedAt,
        string? oldValuesJson,
        string? newValuesJson)
        : base(performedAt)
    {
        if (performedByPersonId <= 0)
        {
            throw new DomainValidationException("Denetim kaydını oluşturan kişi zorunludur.");
        }

        EntityName = Guard.Required(entityName, "Varlık adı", 120);
        EntityId = Guard.Required(entityId, "Varlık kimliği", 80);
        Action = Guard.Required(action, "İşlem", 80);
        PerformedByPersonId = performedByPersonId;
        PerformedAt = performedAt;
        OldValuesJson = Guard.Optional(oldValuesJson, "Eski değerler", 16000);
        NewValuesJson = Guard.Optional(newValuesJson, "Yeni değerler", 16000);
    }

    public string EntityName { get; private set; }
    public string EntityId { get; private set; }
    public string Action { get; private set; }
    public int PerformedByPersonId { get; private set; }
    public DateTimeOffset PerformedAt { get; private set; }
    public string? OldValuesJson { get; private set; }
    public string? NewValuesJson { get; private set; }
}
