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
/// <c>CreatedAt</c> is stamped in UTC by <see cref="Created"/> itself, the same as every other
/// aggregate in this service. <c>CompletedAt</c> is different: it is business data (when the
/// activity was completed), not an audit field, so it comes from the caller through
/// <c>RegisterCompleted</c>'s <c>now</c> parameter instead. <c>UpdatedAt</c> stays null because the
/// legacy table has no updated column, and <c>Id</c> stays 0 until the database generates the
/// identity on save.
/// </para>
/// </remarks>
public sealed class Activity : AggregateRoot<int>
{
    public int DealId { get; }
    public int? OpportunityId { get; }
    public ActivityType Type { get; }

    /// <summary>
    /// Collapses the legacy pair (<c>negact_completada</c>, <c>negact_anulada</c>) — read with
    /// NULL ⇒ false, the permanent read convention of DEC-6 — into a status that makes the
    /// invalid combinations unrepresentable.
    /// </summary>
    public ActivityStatus Status =>
        _isCancelled == true ? ActivityStatus.Cancelled
        : _isCompleted == true ? ActivityStatus.Completed
        : ActivityStatus.Scheduled;

    public Description? Description { get; }
    public DateTime? DueAt { get; }
    public Outcome? Outcome { get; }
    public OutcomeType? OutcomeType { get; private set; }

    /// <summary>
    /// Null only on migrated historic rows read from the legacy database (§4.1); every factory
    /// requires it, so activities created by this service always carry one.
    /// </summary>
    public PersonCode? AdvisorId { get; }

    // negact_per_codigo is NOT NULL in every measured schema variant (0 nulls in data); the
    // null! covers only the parameterless EF materialization constructor.
    public PersonCode CreatedById { get; } = null!;
    public DateTime? CompletedAt { get; }

    // Legacy bit pair behind Status. bool? mirrors the nullable columns so historic NULL rows
    // survive round-trips untouched (DEC-6); the factories always write real booleans.
    private bool? _isCompleted;
    private bool? _isCancelled;

    // EF Core materialization only.
    private Activity() { }

    private Activity(
        int dealId,
        int? opportunityId,
        ActivityType type,
        ActivityStatus status,
        Description? description,
        DateTime? dueAt,
        Outcome? outcome,
        OutcomeType? outcomeType,
        PersonCode advisorId,
        PersonCode createdById,
        DateTime? completedAt)
    {
        DealId = dealId;
        OpportunityId = opportunityId;
        Type = type;
        _isCompleted = status == ActivityStatus.Completed;
        _isCancelled = status == ActivityStatus.Cancelled;
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

        // Only resolve the name for types that carry a coded outcome: for the rest it is
        // discarded silently (legacy parity), and a missing name is the core factory's
        // OutcomeTypeRequired, not a resolution failure.
        OutcomeType? outcomeType = null;
        if (AdmitsOutcomeType(args.Type) && !string.IsNullOrWhiteSpace(args.OutcomeName))
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
        else
        {
            // Legacy parity: the monolith ignores the outcome type for these types instead of
            // rejecting the request.
            outcomeType = null;
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
    /// Persistence-only rehydration. <c>negact_resultado</c> is a char whose meaning depends on
    /// <see cref="Type"/> ('3' is a wrong number for a call but a closed deal for a meeting), so
    /// a value converter — which sees a single column — cannot rebuild the value object. The EF
    /// materialization interceptor resolves it and hands it back through this hook, keeping the
    /// char mapping in Infrastructure (DEC-15). Note: the restored <see cref="OutcomeType.Scope"/>
    /// names the catalogue, so legacy/virtual meeting rows carry Scope = Meeting while Type keeps
    /// their real value — Scope == Type is an invariant of the factories only, not of reads.
    /// </summary>
    internal void RestoreOutcomeType(OutcomeType? outcomeType) => OutcomeType = outcomeType;

    // UpdatedAt intentionally stays null: the legacy table has no updated column.
    protected override void Created() => SetCreatedAt(DateTime.UtcNow);
}
