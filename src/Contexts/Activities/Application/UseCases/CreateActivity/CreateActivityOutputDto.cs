using System.ComponentModel;

namespace Activities.Application.UseCases.CreateActivity;

/// <summary>
/// Response of <c>POST /activities</c>: the generated consecutive, and nothing else — the legacy
/// endpoint's parsimony is preserved on purpose (§6.2).
/// </summary>
public sealed record CreateActivityOutputDto(
    [property: Description("Consecutive the database generated for the new activity.")]
    int Id);
