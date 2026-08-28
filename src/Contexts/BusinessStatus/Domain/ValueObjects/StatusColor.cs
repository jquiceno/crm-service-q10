using BusinessStatus.Domain.Errors;
using Shared.Domain.ValueObjects;
using Shared.Results;
using Shared.Results.Errors;

namespace BusinessStatus.Domain.ValueObjects;

public sealed class StatusColor : ValueObject
{
    public const int Length = 6;

    /// <summary>
    /// The shape a colour must have: <see cref="Length"/> hexadecimal characters, no '#'. It lives
    /// here, next to the rule it describes, so the structural validators of every slice reference the
    /// domain instead of one slice's validator reaching into another's internal constant.
    /// </summary>
    public const string Pattern = "^[0-9A-Fa-f]{6}$";

    public string Value { get; }

    private StatusColor(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Validates a colour written by the user. The absence of colour is represented with a null
    /// aggregate property, never with a default value, so the aggregate only calls this factory
    /// when a value is actually present.
    /// </summary>
    public static Result<StatusColor, ValidationError> Create(string? value)
    {
        if (!IsHexadecimal(value))
            return BusinessStatusErrors.InvalidColorFormat with { Value = value };

        return new StatusColor(value!);
    }

    /// <summary>
    /// Rebuilds the value object from persisted data without validating it: a legacy row may hold
    /// a colour this service would never accept, and reading it must not fail.
    /// </summary>
    internal static StatusColor Reconstruct(string value) => new(value);

    private static bool IsHexadecimal(string? value)
    {
        if (value is null || value.Length != Length)
            return false;

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigit(character))
                return false;
        }

        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
