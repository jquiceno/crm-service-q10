namespace LossReason.Domain.Aggregates;

// IsActive is nullable so an omitted flag reaches the aggregate as "absent" instead of
// silently becoming false: the invariant lives in Create(), not in the CLR default.
public sealed record CreateLossReasonArgs(string? Name, bool? IsActive);

public sealed record UpdateLossReasonArgs(string? Name, bool? IsActive);
