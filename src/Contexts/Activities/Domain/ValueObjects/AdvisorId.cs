using Activities.Domain.Errors;
using Shared.Domain.ValueObjects;
using Shared.Results;
using Shared.Results.Errors;

namespace Activities.Domain.ValueObjects;

/// <summary>
/// Person code of an advisor. Persisted to <c>negact_asesor</c> / <c>negact_per_codigo varchar(20)</c>.
/// </summary>
public sealed class AdvisorId : ValueObject
{
    public string Value { get; } = string.Empty;

    private AdvisorId() { }

    private AdvisorId(string value)
    {
        Value = value;
    }

    public static Result<AdvisorId, ValidationError> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ActivityErrors.AdvisorIdRequired;

        if (value.Length > ActivityLimits.AdvisorIdMaxLength)
            return ActivityErrors.AdvisorIdTooLong;

        return new AdvisorId(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
