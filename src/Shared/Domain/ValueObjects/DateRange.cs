using Shared.Domain.Errors;
using Shared.Results;
using Shared.Results.Errors;

namespace Shared.Domain.ValueObjects;

public sealed class DateRange : ValueObject
{
    public DateTime? Start { get; }
    public DateTime? End { get; }

    private DateRange() { }

    private DateRange(DateTime? start, DateTime? end)
    {
        Start = start;
        End = end;
    }

    public static Result<DateRange, ValidationError> Create(DateTime? start, DateTime? end)
    {
        if (end.HasValue && !start.HasValue)
            return DateRangeErrors.EndDateWithoutStartDate;

        if (start.HasValue && !end.HasValue)
            return DateRangeErrors.StartDateWithoutEndDate;

        if (end.HasValue && start.HasValue && end.Value < start.Value)
            return DateRangeErrors.EndBeforeStart;

        return new DateRange(start, end);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
