using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Enums;
using BusinessStatus.Domain.ValueObjects;
using Shared.Results.Errors;

namespace BusinessStatus.Domain.Errors;

public static class BusinessStatusErrors
{
    public const string Context = "BusinessStatus";

    public static NotFoundError NotFound(int id) =>
        new($"Business status with id '{id}' was not found.");

    public static readonly ValidationError NameRequired =
        new("Business status name is required.", ErrorType.Validation)
        {
            Property = nameof(BusinessStatusAggregate.Name)
        };

    public static readonly ValidationError NameTooLong =
        new($"Business status name must not exceed {BusinessStatusAggregate.MaxNameLength} characters.",
            ErrorType.Validation)
        {
            Property = nameof(BusinessStatusAggregate.Name),
            Attributes = new Dictionary<string, object?>
            {
                ["maxLength"] = BusinessStatusAggregate.MaxNameLength
            }
        };

    public static readonly ValidationError PercentageOutOfRange =
        new($"Percentage must be between {BusinessStatusAggregate.MinPercentage} and {BusinessStatusAggregate.MaxPercentage}.",
            ErrorType.Validation)
        {
            Property = nameof(BusinessStatusAggregate.Percentage),
            Attributes = new Dictionary<string, object?>
            {
                ["min"] = BusinessStatusAggregate.MinPercentage,
                ["max"] = BusinessStatusAggregate.MaxPercentage
            }
        };

    public static readonly ValidationError PercentageMustBeInteger =
        new("Percentage must be a whole number.", ErrorType.Validation)
        {
            Property = nameof(BusinessStatusAggregate.Percentage)
        };

    public static readonly ValidationError TerminalPercentageNotAllowed =
        new($"Percentages {BusinessStatusAggregate.MinPercentage} and {BusinessStatusAggregate.MaxPercentage} are reserved for terminal business statuses.",
            ErrorType.Validation)
        {
            Property = nameof(BusinessStatusAggregate.Percentage)
        };

    public static readonly ValidationError TerminalPercentageIsImmutable =
        new("The percentage of a terminal business status cannot be changed.", ErrorType.Validation)
        {
            Property = nameof(BusinessStatusAggregate.Percentage)
        };

    public static readonly ValidationError InvalidColorFormat =
        new($"Color must be {StatusColor.Length} hexadecimal characters without '#'.", ErrorType.Validation)
        {
            Property = nameof(BusinessStatusAggregate.Color),
            Attributes = new Dictionary<string, object?>
            {
                ["length"] = StatusColor.Length
            }
        };

    public static readonly ConflictError TerminalCannotBeDeleted =
        new("A terminal business status cannot be deleted.");

    public static ConflictError StatusInUse(int id) =>
        new($"Business status with id '{id}' is in use and cannot be deleted.");

    public static ConflictError AmbiguousTerminalStatus(TerminalKind kind) =>
        new($"More than one active '{kind}' business status was found.");

    public static NotFoundError TerminalStatusNotFound(TerminalKind kind) =>
        new($"No active '{kind}' business status was found.");
}
