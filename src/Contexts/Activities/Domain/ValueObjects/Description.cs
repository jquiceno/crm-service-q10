using Activities.Domain.Errors;
using Shared.Domain.ValueObjects;
using Shared.Results;
using Shared.Results.Errors;

namespace Activities.Domain.ValueObjects;

/// <summary>
/// Planned description of a scheduled activity. Persisted to <c>negact_titulo varchar(500)</c>.
/// </summary>
public sealed class Description : ValueObject
{
    public string Value { get; } = string.Empty;

    private Description() { }

    private Description(string value)
    {
        Value = value;
    }

    public static Result<Description, ValidationError> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ActivityErrors.DescriptionRequired;

        if (value.Length > ActivityLimits.DescriptionMaxLength)
            return ActivityErrors.DescriptionTooLong;

        return new Description(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
