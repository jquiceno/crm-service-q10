using System.ComponentModel;

namespace LossReason.Application.UseCases.UpdateLossReason;

// Name and IsActive are nullable on purpose, same as on create: it lets the input validator report
// the error on its own Property instead of the deserializer failing with a generic 400, and it keeps
// an omitted flag from silently becoming false through the CLR default. The invariant lives in
// LossReasonAggregate.Update, not in the type.
public sealed record UpdateLossReasonInputDto(
    [property: Description("New loss reason name. Required; 50 characters at most.")]
    string? Name,
    [property: Description("Whether the loss reason is active. Required.")]
    bool? IsActive);
