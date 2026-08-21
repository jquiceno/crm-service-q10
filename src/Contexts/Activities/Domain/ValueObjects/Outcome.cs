using Activities.Domain.Errors;
using Shared.Domain.ValueObjects;
using Shared.Results;
using Shared.Results.Errors;

namespace Activities.Domain.ValueObjects;

/// <summary>
/// Free-text result of a completed activity.
/// </summary>
/// <remarks>
/// No maximum length on purpose: the logical contract of the column is <c>varchar(MAX)</c> and
/// the domain imposes no cap (DEC-3). The 2000-character limit of the divergent tenants is
/// enforced at the API edge during phase 1.
/// </remarks>
public sealed class Outcome : ValueObject
{
    public string Value { get; } = string.Empty;

    private Outcome() { }

    private Outcome(string value)
    {
        Value = value;
    }

    public static Result<Outcome, ValidationError> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ActivityErrors.OutcomeRequired;

        return new Outcome(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
