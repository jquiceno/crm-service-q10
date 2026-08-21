using Activities.Domain.Aggregates;
using Shared.Results.Errors;

namespace Activities.Domain.Errors;

/// <summary>
/// Validation errors of the Activities context.
/// </summary>
/// <remarks>
/// Each error carries its fixed <c>Property</c> (and the length limits as <c>Attributes</c>) in
/// the definition itself, so the API payload can group by field without parsing messages. The
/// value-object-typed factories return these instances as-is — first violated invariant wins —
/// and tests compare them by identity. The args-based factories accumulate copies enriched via
/// <c>with</c> (<c>Value</c>, and <c>Property</c> for <c>CreatedById</c>). <c>Context</c> and
/// <c>Origin</c> are added at the use case edge.
/// </remarks>
public static class ActivityErrors
{
    public const string Context = "Activities";

    // --- Aggregate invariants -------------------------------------------------------------

    public static readonly ValidationError DealIdRequired =
        new("Deal id is required and must be greater than zero.", ErrorType.Validation)
        {
            Property = nameof(Activity.DealId),
        };

    public static readonly ValidationError InvalidActivityType =
        new("Activity type is not a known value.", ErrorType.Validation)
        {
            Property = nameof(Activity.Type),
        };

    public static readonly ValidationError TypeNotWritable =
        new("Activity type cannot be created by this service.", ErrorType.Validation)
        {
            Property = nameof(Activity.Type),
        };

    public static readonly ValidationError NoteCannotBeScheduled =
        new("A note cannot be scheduled.", ErrorType.Validation)
        {
            Property = nameof(Activity.Type),
        };

    public static readonly ValidationError DescriptionRequired =
        new("Description is required for a scheduled activity.", ErrorType.Validation)
        {
            Property = nameof(Activity.Description),
        };

    public static readonly ValidationError DescriptionTooLong =
        new($"Description cannot exceed {ActivityLimits.DescriptionMaxLength} characters.", ErrorType.Validation)
        {
            Property = nameof(Activity.Description),
            Attributes = new Dictionary<string, object?> { ["maxLength"] = ActivityLimits.DescriptionMaxLength },
        };

    public static readonly ValidationError DueDateRequired =
        new("Due date is required for a scheduled activity.", ErrorType.Validation)
        {
            Property = nameof(Activity.DueAt),
        };

    public static readonly ValidationError OutcomeRequired =
        new("Outcome is required for a completed activity.", ErrorType.Validation)
        {
            Property = nameof(Activity.Outcome),
        };

    public static readonly ValidationError OutcomeTypeRequired =
        new("Outcome type is required for a completed call or meeting.", ErrorType.Validation)
        {
            Property = nameof(Activity.OutcomeType),
        };

    public static readonly ValidationError OutcomeTypeScopeMismatch =
        new("Outcome type does not belong to the activity type's catalogue.", ErrorType.Validation)
        {
            Property = nameof(Activity.OutcomeType),
        };

    // Property defaults to the advisor field, the primary use; the args-based factories override
    // it with CreatedById when the same person-code error applies to that field.

    public static readonly ValidationError PersonCodeRequired =
        new("Person code is required.", ErrorType.Validation)
        {
            Property = nameof(Activity.AdvisorId),
        };

    public static readonly ValidationError PersonCodeTooLong =
        new($"Person code cannot exceed {ActivityLimits.PersonCodeMaxLength} characters.", ErrorType.Validation)
        {
            Property = nameof(Activity.AdvisorId),
            Attributes = new Dictionary<string, object?> { ["maxLength"] = ActivityLimits.PersonCodeMaxLength },
        };

    // --- OutcomeType value object ---------------------------------------------------------

    public static readonly ValidationError UnknownOutcomeType =
        new("Outcome type is not a known value for the activity type.", ErrorType.Validation)
        {
            Property = nameof(Activity.OutcomeType),
        };

    public static readonly ValidationError OutcomeTypeScopeNotSupported =
        new("Only calls and meetings have an outcome type.", ErrorType.Validation)
        {
            Property = nameof(Activity.OutcomeType),
        };

    // --- Raised by the request validator at the API edge ---------------------------------
    // The aggregate makes these structurally impossible: the schedule args carry no outcome and
    // the complete args carry no description. They live here so the API reports them with the
    // same taxonomy.

    public static readonly ValidationError OutcomeNotAllowedWhenScheduled =
        new("Outcome is not allowed for a scheduled activity.", ErrorType.Validation)
        {
            Property = nameof(Activity.Outcome),
        };

    public static readonly ValidationError OutcomeTypeNotAllowedWhenScheduled =
        new("Outcome type is not allowed for a scheduled activity.", ErrorType.Validation)
        {
            Property = nameof(Activity.OutcomeType),
        };

    public static readonly ValidationError DescriptionNotAllowedWhenCompleted =
        new("Description is not allowed for a completed activity.", ErrorType.Validation)
        {
            Property = nameof(Activity.Description),
        };
}
