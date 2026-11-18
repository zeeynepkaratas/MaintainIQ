using MaintainIQ.Domain.Common;

namespace MaintainIQ.Domain.ValueObjects;

public readonly record struct Money
{
    public Money(decimal amount, string currency = "TRY")
    {
        Amount = Guard.NonNegative(amount, "Tutar");
        Currency = NormalizeCurrency(currency);
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency = "TRY") => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(decimal multiplier)
    {
        Guard.NonNegative(multiplier, "Çarpan");
        return new Money(Amount * multiplier, Currency);
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = Guard.Required(currency, "Para birimi", 3).ToUpperInvariant();
        if (normalized.Length != 3)
        {
            throw new DomainValidationException("Para birimi üç harfli ISO kodu olmalıdır.");
        }

        return normalized;
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new DomainValidationException("Farklı para birimleri toplanamaz.");
        }
    }
}
