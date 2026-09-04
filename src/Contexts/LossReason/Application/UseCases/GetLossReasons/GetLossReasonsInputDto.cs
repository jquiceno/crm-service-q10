using System.ComponentModel;

namespace LossReason.Application.UseCases.GetLossReasons;

public sealed record GetLossReasonsInputDto(
    [property: Description("Text to search for. Filters loss reasons whose Name contains it. Optional; 50 characters at most.")]
    string? Search,
    [property: Description("Filters by state: true for active only, false for inactive only. Optional; when omitted, every loss reason is returned.")]
    bool? IsActive);
