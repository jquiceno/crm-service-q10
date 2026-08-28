namespace Activities.Application.UseCases.GetActivities;

/// <summary>
/// One activity as <c>GET /activities</c> returns it (§6.1).
/// </summary>
/// <remarks>
/// Nearly everything is nullable because real legacy rows are: an activity may have no advisor, no
/// due date, no planned description and no outcome, and DEC-6 forbids inventing values for them on
/// read. <c>type</c>, <c>status</c> and <c>outcomeType</c> travel as contract names
/// (<c>call</c>, <c>deal-closed</c>), never as the legacy chars nor the enum's numbers.
/// </remarks>
public sealed record GetActivitiesOutputDto(
    int Id,
    int DealId,
    string? DealName,
    int? OpportunityId,
    string? OpportunityName,
    string Type,
    string Status,
    string? Description,
    string? Outcome,
    string? OutcomeType,
    string? AdvisorId,
    string? AdvisorName,
    string? AdvisorIdentification,
    DateTime? CreatedAt,
    DateTime? DueAt,
    DateTime? CompletedAt);
