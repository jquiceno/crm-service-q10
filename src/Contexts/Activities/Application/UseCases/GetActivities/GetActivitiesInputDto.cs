using System.ComponentModel;

namespace Activities.Application.UseCases.GetActivities;

/// <summary>
/// Query filters of <c>GET /activities</c> (§6.1). Paging travels apart, in <c>PageQuery</c>.
/// </summary>
/// <remarks>
/// All three are nullable and none is required on its own — the contract requires <em>at least
/// one</em> of them, a rule the request validator enforces at the API edge (Tarea 10) so the
/// failure is reported as a 400 with the offending fields, not as an empty page.
/// </remarks>
public sealed record GetActivitiesInputDto(
    [property: Description("Consecutive of the deal whose activities are listed.")]
    int? DealId,
    [property: Description("Consecutive of the opportunity whose activities are listed.")]
    int? OpportunityId,
    [property: Description("Consecutive of the deal state the deals must be in.")]
    int? DealStateId);
