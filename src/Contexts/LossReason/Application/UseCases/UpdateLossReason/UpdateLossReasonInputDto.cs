using System.ComponentModel;

namespace LossReason.Application.UseCases.UpdateLossReason;

// Name is nullable on purpose: it lets the input validator report the error on its own
// Property instead of the deserializer failing with a generic 400.
public sealed record UpdateLossReasonInputDto(
    [property: Description("New loss reason name. Required, up to 50 characters.")]
    string? Name,
    [property: Description("Whether the loss reason is active.")]
    bool IsActive);
