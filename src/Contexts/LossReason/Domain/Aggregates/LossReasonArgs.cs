namespace LossReason.Domain.Aggregates;

public sealed record CreateLossReasonArgs(string? Name, bool IsActive);

public sealed record UpdateLossReasonArgs(string? Name, bool IsActive);
