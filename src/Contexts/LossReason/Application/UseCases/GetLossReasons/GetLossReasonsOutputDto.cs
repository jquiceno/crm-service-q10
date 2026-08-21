using System.ComponentModel;

namespace LossReason.Application.UseCases.GetLossReasons;

public sealed record GetLossReasonsOutputDto(
    [property: Description("Loss reason identifier.")]
    int Id,
    [property: Description("Loss reason name.")]
    string Name,
    [property: Description("Whether the loss reason is active.")]
    bool IsActive);
