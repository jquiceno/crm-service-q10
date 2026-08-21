using System.ComponentModel;

namespace LossReason.Application.UseCases.CreateLossReason;

// Name is nullable on purpose: it lets the input validator report the error on its own
// Property instead of the deserializer failing with a generic 400.
public sealed record CreateLossReasonInputDto(
    [property: Description("Loss reason name. Required; 50 characters at most.")]
    string? Name,
    [property: Description("Whether the loss reason is active. Optional; defaults to true.")]
    bool IsActive = true);
