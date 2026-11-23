using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Domain.Entities;

public sealed class Equipment : BaseEntity
{
    private readonly List<Equipment> _children = [];
    private int? _parentId;

    public Equipment(
        string code,
        string name,
        string location,
        DateTimeOffset createdAt,
        string? qrToken = null)
        : base(createdAt)
    {
        Code = Guard.Required(code, "Ekipman kodu", 50).ToUpperInvariant();
        Name = Guard.Required(name, "Ekipman adı", 160);
        Location = Guard.Required(location, "Konum", 200);
        QrToken = Guard.Optional(qrToken, "QR token", 100);
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string Location { get; private set; }

    public string? QrToken { get; private set; }

    public EquipmentStatus Status { get; private set; } = EquipmentStatus.Operational;

    public Equipment? Parent { get; private set; }

    public int? ParentId => Parent?.Id ?? _parentId;

    public IReadOnlyCollection<Equipment> Children => _children.AsReadOnly();

    public bool IsDeleted { get; private set; }

    internal static Equipment Rehydrate(
        int id,
        int? parentId,
        string code,
        string name,
        string location,
        string? qrToken,
        EquipmentStatus status,
        bool isDeleted,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        byte[] rowVersion)
    {
        var equipment = new Equipment(code, name, location, createdAt, qrToken)
        {
            _parentId = parentId,
            Status = status,
            IsDeleted = isDeleted,
        };
        equipment.RestorePersistenceState(id, createdAt, updatedAt, rowVersion);
        return equipment;
    }

    public void MoveUnder(Equipment? newParent)
    {
        if (ReferenceEquals(this, newParent) || (newParent is not null && IsAncestorOf(newParent)))
        {
            throw new BusinessRuleException("BR-07", "Ekipman kendisine veya bir alt ekipmanına taşınamaz.");
        }

        Parent?._children.Remove(this);
        Parent = newParent;
        _parentId = newParent?.Id;
        if (newParent is not null && !newParent._children.Contains(this))
        {
            newParent._children.Add(this);
        }
    }

    public void MarkFaulty() => ChangeStatus(
        EquipmentStatus.Faulty,
        EquipmentStatus.Operational,
        EquipmentStatus.UnderMaintenance);

    public void MarkUnderMaintenance() => ChangeStatus(
        EquipmentStatus.UnderMaintenance,
        EquipmentStatus.Operational,
        EquipmentStatus.Faulty);

    public void MarkOperational() => ChangeStatus(
        EquipmentStatus.Operational,
        EquipmentStatus.Faulty,
        EquipmentStatus.UnderMaintenance);

    public void Retire() => ChangeStatus(
        EquipmentStatus.Retired,
        EquipmentStatus.Operational,
        EquipmentStatus.Faulty,
        EquipmentStatus.UnderMaintenance);

    public void SoftDelete(bool hasOpenWorkOrders)
    {
        if (_children.Any(child => !child.IsDeleted) || hasOpenWorkOrders)
        {
            throw new BusinessRuleException("BR-08", "Çocuğu veya açık iş emri olan ekipman silinemez.");
        }

        IsDeleted = true;
    }

    public void UpdateDetails(string name, string location, DateTimeOffset changedAt)
    {
        Name = Guard.Required(name, "Ekipman adı", 160);
        Location = Guard.Required(location, "Konum", 200);
        MarkUpdated(changedAt);
    }

    public void SetQrToken(string qrToken, DateTimeOffset changedAt)
    {
        QrToken = Guard.Required(qrToken, "QR token", 100);
        MarkUpdated(changedAt);
    }

    private bool IsAncestorOf(Equipment candidate)
    {
        for (var current = candidate.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    private void ChangeStatus(EquipmentStatus target, params EquipmentStatus[] allowedSources)
    {
        if (!allowedSources.Contains(Status))
        {
            throw new BusinessRuleException(
                "EQ-STATE",
                $"Ekipman durumu {Status} değerinden {target} değerine geçirilemez.");
        }

        Status = target;
    }
}
