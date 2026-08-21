using Shared.Results.Errors;

namespace Activities.Domain.Errors;

/// <summary>
/// Validation errors of the Activities context.
/// </summary>
/// <remarks>
/// The aggregate enriches each error with <c>Property</c> (and <c>Value</c> where useful) via a
/// <c>with</c> expression before accumulating it, so the API payload groups them by field.
/// </remarks>
public static class ActivityErrors
{
    // --- Aggregate invariants -------------------------------------------------------------

    public static readonly ValidationError DealIdRequired =
        new("Deal id is required and must be greater than zero.", ErrorType.Validation);

    public static readonly ValidationError InvalidActivityType =
        new("Activity type is not a known value.", ErrorType.Validation);

    public static readonly ValidationError TypeNotWritable =
        new("Activity type cannot be created by this service.", ErrorType.Validation);

    public static readonly ValidationError NoteCannotBeScheduled =
        new("A note cannot be scheduled.", ErrorType.Validation);

    public static readonly ValidationError DescriptionRequired =
        new("Description is required for a scheduled activity.", ErrorType.Validation);

    public static readonly ValidationError DescriptionTooLong =
        new($"Description cannot exceed {ActivityLimits.DescriptionMaxLength} characters.", ErrorType.Validation);

    public static readonly ValidationError DueDateRequired =
        new("Due date is required for a scheduled activity.", ErrorType.Validation);

    public static readonly ValidationError OutcomeRequired =
        new("Outcome is required for a completed activity.", ErrorType.Validation);

    public static readonly ValidationError OutcomeTypeRequired =
        new("Outcome type is required for a completed call or meeting.", ErrorType.Validation);

    public static readonly ValidationError AdvisorIdRequired =
        new("Person code is required.", ErrorType.Validation);

    public static readonly ValidationError AdvisorIdTooLong =
        new($"Person code cannot exceed {ActivityLimits.AdvisorIdMaxLength} characters.", ErrorType.Validation);

    // --- OutcomeType value object ---------------------------------------------------------

    public static readonly ValidationError UnknownOutcomeType =
        new("Outcome type is not a known value for the activity type.", ErrorType.Validation);

    public static readonly ValidationError OutcomeTypeScopeNotSupported =
        new("Only calls and meetings have an outcome type.", ErrorType.Validation);

    // --- Raised by the request validator at the API edge ---------------------------------
    // The aggregate makes these structurally impossible: the schedule args carry no outcome and
    // the complete args carry no description. They live here so the API reports them with the
    // same taxonomy.

    public static readonly ValidationError OutcomeNotAllowedWhenScheduled =
        new("Outcome is not allowed for a scheduled activity.", ErrorType.Validation);

    public static readonly ValidationError OutcomeTypeNotAllowedWhenScheduled =
        new("Outcome type is not allowed for a scheduled activity.", ErrorType.Validation);

    public static readonly ValidationError DescriptionNotAllowedWhenCompleted =
        new("Description is not allowed for a completed activity.", ErrorType.Validation);
}
