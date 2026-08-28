using Activities.Domain.Enums;
using Activities.Domain.Errors;
using Activities.Domain.ValueObjects;
using Shared.Domain.Aggregates;
using Shared.Results;
using Shared.Results.Errors;

namespace Activities.Domain.Aggregates;

/// <summary>
/// Commercial activity on a deal: a task planned for the future (scheduled) or a fact that
/// already happened (completed). Persisted to <c>tbl_opo_negocios_actividades</c>.
/// </summary>
/// <remarks>
/// The factories make invalid states unrepresentable: <see cref="Schedule(ScheduleActivityArgs)"/>
/// builds the only scheduled shape (description + due date, never an outcome) and
/// <see cref="RegisterCompleted(CompleteActivityArgs, DateTime)"/> the only completed one
/// (outcome, never a planned description). The args-based overloads are the entry point for the
/// application layer: they build the value objects themselves and accumulate every validation
/// error; the value-object-typed overloads hold the invariants and fail fast with the first
/// violated one, returning the <see cref="ActivityErrors"/> instance as-is.
/// <para>
/// <c>CreatedAt</c> is stamped in UTC by <see cref="Created"/>. <c>CompletedAt</c> comes from the
/// caller instead — it's business data, not an audit field. <c>UpdatedAt</c> stays null (no legacy
/// column); <c>Id</c> stays 0 until save.
/// </para>
/// </remarks>
public sealed class Activity : AggregateRoot<int>
{
    public int DealId { get; }
    public int? OpportunityId { get; }
    public ActivityType Type { get; }

    public ActivityStatus Status { get; }

    public Description? Description { get; }
    public DateTime? DueAt { get; }
    public Outcome? Outcome { get; }
    public OutcomeType? OutcomeType { get; }

    /// <summary>
    /// Null only on migrated historic rows read from the legacy database (§4.1); every factory
    /// requires it, so activities created by this service always carry one.
    /// </summary>
    public PersonCode? AdvisorId { get; }

    public PersonCode CreatedById { get; }
    public DateTime? CompletedAt { get; }

    private Activity(
        int dealId,
        int? opportunityId,
        ActivityType type,
        ActivityStatus status,
        Description? description,
        DateTime? dueAt,
        Outcome? outcome,
        OutcomeType? outcomeType,
        PersonCode? advisorId,
        PersonCode createdById,
        DateTime? completedAt)
    {
        DealId = dealId;
        OpportunityId = opportunityId;
        Type = type;
        Status = status;
        Description = description;
        DueAt = dueAt;
        Outcome = outcome;
        OutcomeType = outcomeType;
        AdvisorId = advisorId;
        CreatedById = createdById;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Creates a scheduled activity from application-layer primitives, building the value objects
    /// itself so the caller never handles their <c>Result</c> (see <see cref="ScheduleActivityArgs"/>).
    /// Value object failures are accumulated; invariant failures are reported one at a time.
    /// </summary>
    public static Result<Activity> Schedule(ScheduleActivityArgs args)
    {
        var errors = new List<ValidationError>();

        var description = Collect(Description.Create(args.Description), errors, args.Description);
        var advisorId = Collect(PersonCode.Create(args.AdvisorId), errors, args.AdvisorId);
        var createdById = Collect(
            PersonCode.Create(args.CreatedById), errors, args.CreatedById, nameof(CreatedById));

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var result = Schedule(
            args.DealId, args.OpportunityId, args.Type, description!, args.DueAt,
            advisorId!, createdById!);

        return result.IsFailure
            ? DomainError.FromValidationDomainErrors([result.TypedError])
            : result.Value;
    }

    /// <summary>Creates an activity planned for the future.</summary>
    public static Result<Activity, ValidationError> Schedule(
        int dealId,
        int? opportunityId,
        ActivityType type,
        Description? description,
        DateTime? dueAt,
        PersonCode advisorId,
        PersonCode createdById)
    {
        var guardError = GuardWritable(dealId, type);
        if (guardError is not null)
            return guardError;

        if (type == ActivityType.Note)
            return ActivityErrors.NoteCannotBeScheduled;

        if (description is null)
            return ActivityErrors.DescriptionRequired;

        if (dueAt is null)
            return ActivityErrors.DueDateRequired;

        var activity = new Activity(
            dealId, opportunityId, type, ActivityStatus.Scheduled, description, dueAt,
            outcome: null, outcomeType: null, advisorId, createdById, completedAt: null);
        activity.Created();
        return activity;
    }

    /// <summary>
    /// Records an already-completed activity from application-layer primitives, building the
    /// value objects itself and resolving <see cref="CompleteActivityArgs.OutcomeName"/> against
    /// the catalogue of <see cref="CompleteActivityArgs.Type"/>. Value object failures are
    /// accumulated; invariant failures are reported one at a time.
    /// </summary>
    public static Result<Activity> RegisterCompleted(CompleteActivityArgs args, DateTime now)
    {
        var errors = new List<ValidationError>();

        var outcome = Collect(Outcome.Create(args.Outcome), errors, args.Outcome);
        var advisorId = Collect(PersonCode.Create(args.AdvisorId), errors, args.AdvisorId);
        var createdById = Collect(
            PersonCode.Create(args.CreatedById), errors, args.CreatedById, nameof(CreatedById));

        // A missing name for a type that admits an outcome is the core factory's
        // OutcomeTypeRequired, not a resolution failure here. A name for a type that doesn't
        // admit one is rejected by OutcomeType.Create's own scope check (DEC-13).
        OutcomeType? outcomeType = null;
        if (!string.IsNullOrWhiteSpace(args.OutcomeName))
            outcomeType = Collect(
                OutcomeType.Create(args.Type, args.OutcomeName), errors, args.OutcomeName);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var result = RegisterCompleted(
            args.DealId, args.OpportunityId, args.Type, outcome!, outcomeType, args.DueAt,
            advisorId!, createdById!, now);

        return result.IsFailure
            ? DomainError.FromValidationDomainErrors([result.TypedError])
            : result.Value;
    }

    /// <summary>Records an activity that already happened, born completed.</summary>
    public static Result<Activity, ValidationError> RegisterCompleted(
        int dealId,
        int? opportunityId,
        ActivityType type,
        Outcome? outcome,
        OutcomeType? outcomeType,
        DateTime? dueAt,
        PersonCode advisorId,
        PersonCode createdById,
        DateTime now)
    {
        var guardError = GuardWritable(dealId, type);
        if (guardError is not null)
            return guardError;

        if (outcome is null)
            return ActivityErrors.OutcomeRequired;

        if (AdmitsOutcomeType(type))
        {
            if (outcomeType is null)
                return ActivityErrors.OutcomeTypeRequired;

            if (outcomeType.Scope != type)
                return ActivityErrors.OutcomeTypeScopeMismatch;
        }
        else if (outcomeType is not null)
        {
            return ActivityErrors.OutcomeTypeScopeNotSupported;
        }

        var activity = new Activity(
            dealId, opportunityId, type, ActivityStatus.Completed, description: null, dueAt,
            outcome, outcomeType, advisorId, createdById, completedAt: now);
        activity.Created();
        return activity;
    }

    /// <summary>
    /// True for the types this service can write. <see cref="ActivityType.VirtualMeeting"/> and
    /// <see cref="ActivityType.LegacyMeeting"/> are returned on reads but never created (DEC-5).
    /// </summary>
    public static bool IsWritable(ActivityType type) =>
        type is ActivityType.Call or ActivityType.WhatsApp or ActivityType.Email
            or ActivityType.Note or ActivityType.Meeting;

    /// <summary>Only calls and meetings carry a coded outcome type.</summary>
    public static bool AdmitsOutcomeType(ActivityType type) =>
        type is ActivityType.Call or ActivityType.Meeting;

    private static ValidationError? GuardWritable(int dealId, ActivityType type)
    {
        if (dealId <= 0)
            return ActivityErrors.DealIdRequired;

        if (!Enum.IsDefined(type))
            return ActivityErrors.InvalidActivityType;

        if (!IsWritable(type))
            return ActivityErrors.TypeNotWritable;

        return null;
    }

    /// <summary>
    /// Unwraps a value object result, accumulating the failure enriched with the raw input and,
    /// when the error definition is shared between fields, the actual property name.
    /// </summary>
    private static T? Collect<T>(
        Result<T, ValidationError> result,
        List<ValidationError> errors,
        object? value,
        string? property = null)
        where T : class
    {
        if (result.IsSuccess)
            return result.Value;

        errors.Add(result.TypedError with
        {
            Property = property ?? result.TypedError.Property,
            Value = value,
        });
        return null;
    }

    /// <summary>
    /// Rebuilds the aggregate from persistence without validation or audit stamping (the
    /// template's reconstruction factory): a legacy row is valid data even where today's
    /// creation invariants would reject it — missing advisor, read-only types, rows carrying
    /// both <see cref="Description"/> and <see cref="Outcome"/> at once, or an
    /// <see cref="OutcomeType"/> whose <see cref="OutcomeType.Scope"/> names the catalogue
    /// (Meeting) while <see cref="Type"/> keeps the real legacy/virtual value. Only the
    /// persistence mapper calls it; Scope == Type is an invariant of the factories, not of reads.
    /// The identity is the one exception: it defines equality (<c>Entity&lt;TId&gt;</c>), so a
    /// non-positive one is a programming error, never legacy data.
    /// </summary>
    internal static Activity Reconstruct(
        int id,
        int dealId,
        int? opportunityId,
        ActivityType type,
        ActivityStatus status,
        Description? description,
        DateTime? dueAt,
        Outcome? outcome,
        OutcomeType? outcomeType,
        PersonCode? advisorId,
        PersonCode createdById,
        DateTime createdAt,
        DateTime? completedAt)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(id), id, "A persisted activity always has a positive identity.");

        var activity = new Activity(
            dealId, opportunityId, type, status, description, dueAt, outcome, outcomeType,
            advisorId, createdById, completedAt)
        {
            Id = id,
        };
        activity.SetCreatedAt(createdAt);
        return activity;
    }

    /// <summary>Assigns the id SQL Server generated on insert (called only by the repository).</summary>
    internal void AssignId(int id) => Id = id;

    // UpdatedAt intentionally stays null: the legacy table has no updated column.
    protected override void Created() => SetCreatedAt(DateTime.UtcNow);
}
