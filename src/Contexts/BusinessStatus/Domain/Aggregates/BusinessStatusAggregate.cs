using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.ValueObjects;
using Shared.Domain.Aggregates;
using Shared.Results;
using Shared.Results.Errors;

namespace BusinessStatus.Domain.Aggregates;

public sealed class BusinessStatusAggregate : AggregateRoot<int>
{
    public const int MinPercentage = 0;
    public const int MaxPercentage = 100;
    public const int MaxNameLength = 200;

    public string Name { get; private set; }
    public int? Percentage { get; private set; }
    public StatusColor? Color { get; private set; }
    public bool IsActive { get; private set; }

    public bool IsWon => Percentage == MaxPercentage;
    public bool IsLost => Percentage == MinPercentage;
    public bool IsTerminal => IsWon || IsLost;
    public bool IsIntermediate => !IsTerminal;

    private BusinessStatusAggregate(int id, string name, int? percentage, StatusColor? color, bool isActive)
    {
        Id = id;
        Name = name;
        Percentage = percentage;
        Color = color;
        IsActive = isActive;
    }

    public static Result<BusinessStatusAggregate> Create(CreateBusinessStatusArgs args)
    {
        var errors = new List<ValidationError>();

        var nameError = ValidateName(args.Name);
        if (nameError is not null)
            errors.Add(nameError);

        var percentageResult = ValidatePercentageForCreate(args.Percentage);
        if (percentageResult.IsFailure)
            errors.Add(percentageResult.TypedError);

        var colorResult = ValidateColor(args.Color);
        if (colorResult.IsFailure)
            errors.Add(colorResult.TypedError);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var aggregate = new BusinessStatusAggregate(
            id: default,
            name: args.Name!.Trim(),
            percentage: percentageResult.Value,
            color: colorResult.Value,
            isActive: args.IsActive);

        aggregate.Created();

        return aggregate;
    }

    public Result<BusinessStatusAggregate> Update(UpdateBusinessStatusArgs args)
    {
        var errors = new List<ValidationError>();

        var nameError = ValidateName(args.Name);
        if (nameError is not null)
            errors.Add(nameError);

        var percentageResult = ValidatePercentageForUpdate(args.Percentage);
        if (percentageResult.IsFailure)
            errors.Add(percentageResult.TypedError);

        var colorResult = ValidateColor(args.Color);
        if (colorResult.IsFailure)
            errors.Add(colorResult.TypedError);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        Name = args.Name!.Trim();
        Percentage = percentageResult.Value;
        Color = colorResult.Value;
        IsActive = args.IsActive;
        SetUpdatedAt(DateTime.UtcNow);

        return this;
    }

    public Result EnsureCanBeDeleted()
    {
        if (IsTerminal)
            return BusinessStatusErrors.TerminalCannotBeDeleted;

        return Result.Success();
    }

    /// <summary>
    /// Receives the identifier the database generated on insert, which only exists after the
    /// statement runs. It lets the repository complete the very aggregate it was given instead of
    /// building a second one, so whatever <c>Create</c> set — the audit timestamps among it —
    /// survives the round trip.
    /// </summary>
    public void AssignId(int id) => Id = id;

    /// <summary>
    /// Rebuilds the aggregate from persisted data. It validates nothing: a legacy row may hold a name
    /// longer than <see cref="MaxNameLength"/>, a colour this service would never accept or a terminal
    /// percentage, and reading it must not fail. The decimal-to-integer conversion of the real column
    /// already happened in the repository mapper.
    /// </summary>
    public static BusinessStatusAggregate Reconstruct(
        int id, string name, int? percentage, string? color, bool isActive) =>
        new(id,
            name,
            percentage,
            string.IsNullOrEmpty(color) ? null : StatusColor.Reconstruct(color),
            isActive);

    protected override void Created()
    {
        SetCreatedAt(DateTime.UtcNow);
        SetUpdatedAt(DateTime.UtcNow);
    }

    private static ValidationError? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BusinessStatusErrors.NameRequired;

        if (name.Trim().Length > MaxNameLength)
            return BusinessStatusErrors.NameTooLong with { Value = name };

        return null;
    }

    private static Result<StatusColor?, ValidationError> ValidateColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return Result<StatusColor?, ValidationError>.Success(null);

        var result = StatusColor.Create(color);
        if (result.IsFailure)
            return result.TypedError;

        return result.Value;
    }

    private static Result<int, ValidationError> ValidatePercentageForCreate(decimal value)
    {
        var percentage = ToWholePercentage(value);
        if (percentage.IsFailure)
            return percentage.TypedError;

        if (percentage.Value == MinPercentage || percentage.Value == MaxPercentage)
            return BusinessStatusErrors.TerminalPercentageNotAllowed with { Value = value };

        return percentage.Value;
    }

    private Result<int, ValidationError> ValidatePercentageForUpdate(decimal value)
    {
        if (!IsTerminal)
            return ValidatePercentageForCreate(value);

        var percentage = ToWholePercentage(value);
        if (percentage.IsFailure)
            return percentage.TypedError;

        if (percentage.Value != Percentage)
            return BusinessStatusErrors.TerminalPercentageIsImmutable with { Value = value };

        return percentage.Value;
    }

    private static Result<int, ValidationError> ToWholePercentage(decimal value)
    {
        if (value < MinPercentage || value > MaxPercentage)
            return BusinessStatusErrors.PercentageOutOfRange with { Value = value };

        if (decimal.Truncate(value) != value)
            return BusinessStatusErrors.PercentageMustBeInteger with { Value = value };

        return (int)value;
    }
}
