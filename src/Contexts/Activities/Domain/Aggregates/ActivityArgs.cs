using Activities.Domain.Enums;

namespace Activities.Domain.Aggregates;

/// <summary>
/// Arguments to schedule an activity.
/// </summary>
/// <remarks>
/// Primitives and domain enums only — never value objects. The factory builds the value objects
/// itself, so the application layer never handles their <c>Result</c>.
/// <para>
/// There is one record per creation operation because the two flows carry different fields: a
/// scheduled activity has a description and a due date, a completed one has an outcome.
/// </para>
/// </remarks>
public sealed record ScheduleActivityArgs(
    int DealId,
    int? OpportunityId,
    ActivityType Type,
    string? Description,
    DateTime? DueAt,
    string? AdvisorId,
    string? CreatedById);

/// <summary>
/// Arguments to record an activity that already happened.
/// </summary>
/// <remarks>
/// <paramref name="OutcomeName"/> is the member name of <see cref="CallOutcome"/> or
/// <see cref="MeetingOutcome"/>; the factory resolves it against the catalogue that matches
/// <paramref name="Type"/>.
/// </remarks>
public sealed record CompleteActivityArgs(
    int DealId,
    int? OpportunityId,
    ActivityType Type,
    string? Outcome,
    string? OutcomeName,
    DateTime? DueAt,
    string? AdvisorId,
    string? CreatedById);
