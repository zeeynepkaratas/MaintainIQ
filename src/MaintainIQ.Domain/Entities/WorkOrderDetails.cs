using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Domain.Entities;

public sealed class WorkOrderPart : BaseEntity
{
    public WorkOrderPart(int sparePartId, decimal quantity, Money unitCostSnapshot, DateTimeOffset createdAt)
        : base(createdAt)
    {
        if (sparePartId <= 0)
        {
            throw new DomainValidationException("Geçerli bir yedek parça seçilmelidir.");
        }

        SparePartId = sparePartId;
        Quantity = Guard.Positive(quantity, "Parça miktarı");
        UnitCostSnapshot = unitCostSnapshot;
    }

    public int SparePartId { get; private set; }
    public decimal Quantity { get; private set; }
    public Money UnitCostSnapshot { get; private set; }
    public Money TotalCost => UnitCostSnapshot.Multiply(Quantity);

    internal static WorkOrderPart Rehydrate(
        int id, int sparePartId, decimal quantity, decimal unitCost, string currency,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, byte[] rowVersion)
    {
        var part = new WorkOrderPart(sparePartId, quantity, new Money(unitCost, currency), createdAt);
        part.RestorePersistenceState(id, createdAt, updatedAt, rowVersion);
        return part;
    }
}

public sealed class WorkOrderStatusHistoryEntry : BaseEntity
{
    public WorkOrderStatusHistoryEntry(
        WorkOrderStatus fromStatus,
        WorkOrderStatus toStatus,
        int changedByPersonId,
        DateTimeOffset changedAt,
        string? reason = null)
        : base(changedAt)
    {
        if (changedByPersonId <= 0)
        {
            throw new DomainValidationException("Durum değişikliğini yapan kişi zorunludur.");
        }

        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedByPersonId = changedByPersonId;
        ChangedAt = changedAt;
        Reason = Guard.Optional(reason, "Durum değişikliği gerekçesi", 1000);
    }

    public WorkOrderStatus FromStatus { get; private set; }
    public WorkOrderStatus ToStatus { get; private set; }
    public int ChangedByPersonId { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public string? Reason { get; private set; }
}

public sealed class WorkOrderComment : BaseEntity
{
    public WorkOrderComment(int authorPersonId, string text, DateTimeOffset createdAt)
        : base(createdAt)
    {
        if (authorPersonId <= 0)
        {
            throw new DomainValidationException("Yorum yazarı zorunludur.");
        }

        AuthorPersonId = authorPersonId;
        Text = Guard.Required(text, "Yorum", 2000);
    }

    public int AuthorPersonId { get; private set; }
    public string Text { get; private set; }
}
