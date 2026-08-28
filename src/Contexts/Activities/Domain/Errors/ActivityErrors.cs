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
            Property = nameof(ActivityAggregate.DealId),
        };

    public static readonly ValidationError InvalidActivityType =
        new("Activity type is not a known value.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Type),
        };

    public static readonly ValidationError TypeNotWritable =
        new("Activity type cannot be created by this service.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Type),
        };

    public static readonly ValidationError NoteCannotBeScheduled =
        new("A note cannot be scheduled.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Type),
        };

    public static readonly ValidationError DescriptionRequired =
        new("Description is required for a scheduled activity.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Description),
        };

    public static readonly ValidationError DescriptionTooLong =
        new($"Description cannot exceed {ActivityLimits.DescriptionMaxLength} characters.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Description),
            Attributes = new Dictionary<string, object?> { ["maxLength"] = ActivityLimits.DescriptionMaxLength },
        };

    public static readonly ValidationError DueDateRequired =
        new("Due date is required for a scheduled activity.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.DueAt),
        };

    public static readonly ValidationError OutcomeRequired =
        new("Outcome is required for a completed activity.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Outcome),
        };

    public static readonly ValidationError OutcomeTypeRequired =
        new("Outcome type is required for a completed call or meeting.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.OutcomeType),
        };

    public static readonly ValidationError OutcomeTypeScopeMismatch =
        new("Outcome type does not belong to the activity type's catalogue.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.OutcomeType),
        };

    // Property defaults to the advisor field, the primary use; the args-based factories override
    // it with CreatedById when the same person-code error applies to that field.

    public static readonly ValidationError PersonCodeRequired =
        new("Person code is required.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.AdvisorId),
        };

    public static readonly ValidationError PersonCodeTooLong =
        new($"Person code cannot exceed {ActivityLimits.PersonCodeMaxLength} characters.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.AdvisorId),
            Attributes = new Dictionary<string, object?> { ["maxLength"] = ActivityLimits.PersonCodeMaxLength },
        };

    // --- OutcomeType value object ---------------------------------------------------------

    public static readonly ValidationError UnknownOutcomeType =
        new("Outcome type is not a known value for the activity type.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.OutcomeType),
        };

    public static readonly ValidationError OutcomeTypeScopeNotSupported =
        new("Only calls and meetings have an outcome type.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.OutcomeType),
        };

    // --- Raised by the request validator at the API edge ---------------------------------
    // The aggregate makes these structurally impossible: the schedule args carry no outcome and
    // the complete args carry no description. They live here so the API reports them with the
    // same taxonomy.

    public static readonly ValidationError OutcomeNotAllowedWhenScheduled =
        new("Outcome is not allowed for a scheduled activity.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Outcome),
        };

    public static readonly ValidationError OutcomeTypeNotAllowedWhenScheduled =
        new("Outcome type is not allowed for a scheduled activity.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.OutcomeType),
        };

    public static readonly ValidationError DescriptionNotAllowedWhenCompleted =
        new("Description is not allowed for a completed activity.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Description),
        };

    // --- Raised by the use cases when resolving the request against the institution ----------

    public static readonly ValidationError InvalidActivityStatus =
        new("Activity status is not a known value.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Status),
        };

    /// <summary>
    /// Distinct from <see cref="InvalidActivityStatus"/> on purpose: <c>cancelled</c> is a real
    /// status of the domain, it just cannot be the status an activity is born with.
    /// </summary>
    public static readonly ValidationError StatusNotCreatable =
        new("Only 'scheduled' and 'completed' activities can be created.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.Status),
        };

    /// <summary>
    /// The deal's opportunity is archived. A validation error, not a conflict: the legacy API
    /// answered 400 here and the strangler keeps that status (§6.x).
    /// </summary>
    public static readonly ValidationError OpportunityArchived =
        new("The deal's opportunity is archived.", ErrorType.Validation)
        {
            Property = nameof(ActivityAggregate.DealId),
        };

    public static NotFoundError DealNotFound(int dealId) =>
        new($"Deal with id '{dealId}' was not found.");

    /// <summary>
    /// No person carries that identification. Whether the person exists but lacks the advisor role
    /// is not asked here — that check belongs to the caller (DEC-17).
    /// </summary>
    public static NotFoundError AdvisorNotFound(string? identification) =>
        new($"Advisor with identification '{identification}' was not found.");

    // --- Persistence ------------------------------------------------------------------------

    public static NotFoundError NotFound(int id) =>
        new($"Activity with id '{id}' was not found.");
}
