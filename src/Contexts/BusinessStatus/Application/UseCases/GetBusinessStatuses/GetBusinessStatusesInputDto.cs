using System.ComponentModel;
using BusinessStatus.Domain.Enums;

namespace BusinessStatus.Application.UseCases.GetBusinessStatuses;

public sealed record GetBusinessStatusesInputDto(
    [property: Description("Partial match on the stage name, equivalent to LIKE '%text%'. Omitted means no filter.")]
    string? Name = null,
    [property: Description("Filters by activity. Omitted means no filter: active and inactive stages are returned alike.")]
    bool? IsActive = null,
    [property: Description("Stage filter: All (default), Intermediate for everything that is neither 0 nor 100, or Terminal for those two.")]
    BusinessStatusKind? Kind = null);
