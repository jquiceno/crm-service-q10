using Activities.Domain.Enums;
using Activities.Domain.Errors;
using Shared.Domain.ValueObjects;
using Shared.Results;
using Shared.Results.Errors;

namespace Activities.Domain.ValueObjects;

/// <summary>
/// Coded outcome of a completed call or meeting. Persisted to <c>negact_resultado</c>.
/// </summary>
/// <remarks>
/// The legacy column is a single <c>char(1)</c> whose meaning depends on the activity type —
/// <c>'3'</c> is "wrong number" for a call and "deal closed" for a meeting. Two separate enums
/// cannot live in one field, so this value object carries the scope together with the value and
/// is the only type the aggregate stores.
/// <para>
/// It holds no legacy char: the persistence converter maps the pair
/// (<see cref="Scope"/>, <see cref="Name"/>) to the column and back, which keeps the char out of
/// the domain (DEC-15) while still giving the converter everything it needs from a single
/// property.
/// </para>
/// </remarks>
public sealed class OutcomeType : ValueObject
{
    /// <summary>Activity type this outcome belongs to. Only Call and Meeting are supported.</summary>
    public ActivityType Scope { get; }

    /// <summary>Member name of <see cref="CallOutcome"/> or <see cref="MeetingOutcome"/>.</summary>
    public string Name { get; } = string.Empty;

    /// <summary>
    /// True for the reserved "deal closed" outcome, written by the automatic close of a deal.
    /// Both catalogues spell it identically, so one comparison covers either scope.
    /// </summary>
    public bool IsDealClosed =>
        string.Equals(Name, nameof(CallOutcome.DealClosed), StringComparison.Ordinal);

    private OutcomeType() { }

    private OutcomeType(ActivityType scope, string name)
    {
        Scope = scope;
        Name = name;
    }

    public static Result<OutcomeType, ValidationError> ForCall(CallOutcome value)
    {
        return Enum.IsDefined(value)
            ? new OutcomeType(ActivityType.Call, value.ToString())
            : ActivityErrors.UnknownOutcomeType;
    }

    public static Result<OutcomeType, ValidationError> ForMeeting(MeetingOutcome value)
    {
        return Enum.IsDefined(value)
            ? new OutcomeType(ActivityType.Meeting, value.ToString())
            : ActivityErrors.UnknownOutcomeType;
    }

    /// <summary>
    /// Resolves an outcome from its member name, scoped to an activity type. Used by the API edge
    /// and by the persistence converter.
    /// </summary>
    public static Result<OutcomeType, ValidationError> Create(ActivityType scope, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ActivityErrors.UnknownOutcomeType;

        var candidate = name.Trim();

        // Enum.TryParse also accepts the underlying number ("1" -> NoAnswer), which would leak a
        // numeric coupling the domain does not want. Only member names are accepted.
        if (!char.IsLetter(candidate[0]))
            return ActivityErrors.UnknownOutcomeType;

        switch (scope)
        {
            case ActivityType.Call:
                return Enum.TryParse<CallOutcome>(candidate, ignoreCase: true, out var call)
                    ? ForCall(call)
                    : ActivityErrors.UnknownOutcomeType;

            case ActivityType.Meeting:
                return Enum.TryParse<MeetingOutcome>(candidate, ignoreCase: true, out var meeting)
                    ? ForMeeting(meeting)
                    : ActivityErrors.UnknownOutcomeType;

            default:
                return ActivityErrors.OutcomeTypeScopeNotSupported;
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Scope;
        yield return Name;
    }
}
