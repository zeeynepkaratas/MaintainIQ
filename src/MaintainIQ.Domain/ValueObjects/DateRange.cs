using MaintainIQ.Domain.Common;

namespace MaintainIQ.Domain.ValueObjects;

public readonly record struct DateRange
{
    public DateRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new DomainValidationException("Bitiş zamanı başlangıç zamanından sonra olmalıdır.");
        }

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    public bool Overlaps(DateRange other) => Start < other.End && other.Start < End;
}
