using System.ComponentModel;

namespace LossReason.Application.UseCases.GetLossReasons;

public sealed record GetLossReasonsInputDto(
    [property: Description("Filters loss reasons whose name contains this text. Optional; 50 characters at most.")]
    string? Name,
    [property: Description("Filters by state: true for active only, false for inactive only. Optional; when omitted, every loss reason is returned.")]
    bool? IsActive);
