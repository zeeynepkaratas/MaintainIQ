using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;

namespace MaintainIQ.Domain.Entities;

public sealed class Supplier : BaseEntity
{
    public Supplier(string code, string name, DateTimeOffset createdAt, string? contact = null)
        : base(createdAt)
    {
        Code = Guard.Required(code, "Tedarikçi kodu", 40).ToUpperInvariant();
        Name = Guard.Required(name, "Tedarikçi adı", 160);
        Contact = Guard.Optional(contact, "İletişim bilgisi", 300);
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Contact { get; private set; }
    public bool IsActive { get; private set; } = true;
}

public sealed class SparePart : BaseEntity
{
    public SparePart(
        string code,
        string name,
        decimal stockQuantity,
        decimal minimumStock,
        Money unitCost,
        DateTimeOffset createdAt,
        int? supplierId = null)
        : base(createdAt)
    {
        Code = Guard.Required(code, "Parça kodu", 50).ToUpperInvariant();
        Name = Guard.Required(name, "Parça adı", 160);
        StockQuantity = Guard.NonNegative(stockQuantity, "Stok miktarı");
        MinimumStock = Guard.NonNegative(minimumStock, "Minimum stok");
        UnitCost = unitCost;
        SupplierId = supplierId;
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public decimal StockQuantity { get; private set; }
    public decimal MinimumStock { get; private set; }
    public Money UnitCost { get; private set; }
    public int? SupplierId { get; private set; }
    public bool IsBelowMinimum => StockQuantity < MinimumStock;

    internal static SparePart Rehydrate(
        int id, int? supplierId, string code, string name, decimal stockQuantity,
        decimal minimumStock, decimal unitCost, string currency,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, byte[] rowVersion)
    {
        var part = new SparePart(
            code, name, stockQuantity, minimumStock, new Money(unitCost, currency), createdAt, supplierId);
        part.RestorePersistenceState(id, createdAt, updatedAt, rowVersion);
        return part;
    }

    public void Receive(decimal quantity, Money unitCost)
    {
        quantity = Guard.Positive(quantity, "Stok giriş miktarı");
        if (!string.Equals(UnitCost.Currency, unitCost.Currency, StringComparison.Ordinal))
        {
            throw new DomainValidationException("Stok giriş maliyetinin para birimi parça ile aynı olmalıdır.");
        }

        StockQuantity += quantity;
        UnitCost = unitCost;
    }

    public Money Consume(decimal quantity)
    {
        quantity = Guard.Positive(quantity, "Kullanım miktarı");
        if (quantity > StockQuantity)
        {
            throw new BusinessRuleException("BR-02", "Yetersiz stok nedeniyle parça kullanılamaz.");
        }

        StockQuantity -= quantity;
        return UnitCost;
    }

    public void Adjust(decimal newQuantity, string reason)
    {
        Guard.Required(reason, "Stok düzeltme gerekçesi", 500);
        StockQuantity = Guard.NonNegative(newQuantity, "Yeni stok miktarı");
    }
}

public sealed class StockMovement : BaseEntity
{
    public StockMovement(
        int sparePartId,
        StockMovementType type,
        decimal quantity,
        decimal resultingQuantity,
        Money unitCost,
        DateTimeOffset createdAt,
        string reason,
        int? workOrderId = null)
        : base(createdAt)
    {
        if (sparePartId <= 0)
        {
            throw new DomainValidationException("Stok hareketi için geçerli bir parça seçilmelidir.");
        }

        SparePartId = sparePartId;
        Type = type;
        Quantity = Guard.Positive(quantity, "Hareket miktarı");
        ResultingQuantity = Guard.NonNegative(resultingQuantity, "Hareket sonrası stok");
        UnitCost = unitCost;
        Reason = Guard.Required(reason, "Stok hareketi gerekçesi", 500);
        WorkOrderId = workOrderId;
    }

    public int SparePartId { get; private set; }
    public StockMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal ResultingQuantity { get; private set; }
    public Money UnitCost { get; private set; }
    public string Reason { get; private set; }
    public int? WorkOrderId { get; private set; }

    internal static StockMovement Rehydrate(
        int id, int sparePartId, int? workOrderId, StockMovementType type,
        decimal quantity, decimal resultingQuantity, decimal unitCost, string currency,
        string reason, DateTimeOffset createdAt, DateTimeOffset updatedAt, byte[] rowVersion)
    {
        var movement = new StockMovement(
            sparePartId, type, quantity, resultingQuantity, new Money(unitCost, currency),
            createdAt, reason, workOrderId);
        movement.RestorePersistenceState(id, createdAt, updatedAt, rowVersion);
        return movement;
    }
}
