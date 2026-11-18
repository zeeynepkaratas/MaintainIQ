namespace MaintainIQ.Domain.Common;

public abstract class BaseEntity
{
    protected BaseEntity(DateTimeOffset createdAt)
    {
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public int Id { get; protected set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void MarkUpdated(DateTimeOffset updatedAt)
    {
        if (updatedAt < CreatedAt)
        {
            throw new DomainValidationException("Güncelleme zamanı oluşturma zamanından önce olamaz.");
        }

        UpdatedAt = updatedAt;
    }
}
