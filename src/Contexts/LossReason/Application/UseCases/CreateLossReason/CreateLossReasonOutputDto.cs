using System.ComponentModel;

namespace LossReason.Application.UseCases.CreateLossReason;

public sealed record CreateLossReasonOutputDto(
    [property: Description("Identifier assigned to the created loss reason.")]
    int Id,
    [property: Description("Loss reason name.")]
    string Name,
    [property: Description("Whether the loss reason is active.")]
    bool IsActive);
