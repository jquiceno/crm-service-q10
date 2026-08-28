using Activities.Domain.Errors;
using Shared.Domain.ValueObjects;
using Shared.Results;
using Shared.Results.Errors;

namespace Activities.Domain.ValueObjects;

/// <summary>
/// Code that identifies a person in the legacy institution database. Role-neutral on purpose:
/// the aggregate uses it both for the advisor (<c>negact_asesor</c> / <c>negact_per_codigo
/// varchar(20)</c>) and for the user who registers the activity — the property name, not the
/// type, expresses the role.
/// </summary>
public sealed class PersonCode : ValueObject
{
    public string Value { get; } = string.Empty;

    private PersonCode() { }

    private PersonCode(string value)
    {
        Value = value;
    }

    public static Result<PersonCode, ValidationError> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ActivityErrors.PersonCodeRequired;

        if (value.Length > ActivityLimits.PersonCodeMaxLength)
            return ActivityErrors.PersonCodeTooLong;

        return new PersonCode(value);
    }

    /// <summary>
    /// Rebuilds the value object from persistence without validation: stored values are
    /// legitimate legacy data even when today's creation rules would reject them (DEC-6).
    /// </summary>
    internal static PersonCode Reconstruct(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
