namespace MaintainIQ.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainValidationException($"{fieldName} zorunludur.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainValidationException($"{fieldName} en fazla {maxLength} karakter olabilir.");
        }

        return normalized;
    }

    public static string? Optional(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainValidationException($"{fieldName} en fazla {maxLength} karakter olabilir.");
        }

        return normalized;
    }

    public static decimal Positive(decimal value, string fieldName)
    {
        if (value <= 0)
        {
            throw new DomainValidationException($"{fieldName} sıfırdan büyük olmalıdır.");
        }

        return value;
    }

    public static decimal NonNegative(decimal value, string fieldName)
    {
        if (value < 0)
        {
            throw new DomainValidationException($"{fieldName} negatif olamaz.");
        }

        return value;
    }
}
