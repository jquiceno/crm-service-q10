using Shared.Results.Errors;

namespace Shared.Domain.Errors;

public static class DateRangeErrors
{
    public static readonly ValidationError EndBeforeStart =
        new("End date must be greater than or equal to start date.", ErrorType.Validation);

    public static readonly ValidationError EndDateWithoutStartDate =
        new("Start date is required when end date is provided.", ErrorType.Validation);

    public static readonly ValidationError StartDateWithoutEndDate =
        new("End date is required when start date is provided.", ErrorType.Validation);
}
