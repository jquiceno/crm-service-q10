using System.ComponentModel;

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
    [property: Description("Consecutive of the activity.")]
    int Id,
    [property: Description("Consecutive of the deal the activity belongs to.")]
    int DealId,
    [property: Description("Name of the deal. Null if the deal has none.")]
    string? DealName,
    [property: Description("Consecutive of the deal's opportunity.")]
    int? OpportunityId,
    [property: Description("Name of the opportunity. Null if it has none.")]
    string? OpportunityName,
    [property: Description("Kind of interaction: 'call', 'whatsapp', 'email', 'note', 'meeting' or 'virtual-meeting'.")]
    string Type,
    [property: Description("Lifecycle state: 'scheduled', 'completed' or 'cancelled'.")]
    string Status,
    [property: Description("What was planned. Null on completed activities and on legacy rows without one.")]
    string? Description,
    [property: Description("What happened. Null while the activity is still scheduled.")]
    string? Outcome,
    [property: Description("Coded outcome, e.g. 'contacted' or 'deal-closed'. Only calls and meetings carry one.")]
    string? OutcomeType,
    [property: Description("Person code of the advisor responsible. Null on migrated history.")]
    string? AdvisorId,
    [property: Description("Full name of the advisor. Null if there is no advisor.")]
    string? AdvisorName,
    [property: Description("Identification number of the advisor. Null if there is no advisor.")]
    string? AdvisorIdentification,
    [property: Description("When the activity was recorded.")]
    DateTime? CreatedAt,
    [property: Description("When the activity is due. Only scheduled activities carry one.")]
    DateTime? DueAt,
    [property: Description("When the activity was completed.")]
    DateTime? CompletedAt);
