namespace BusinessStatus.Domain.Aggregates;

/// <summary>
/// Primitive arguments for a full replacement of an existing business status: every field travels,
/// so the update never leaves a column behind.
/// </summary>
public sealed record UpdateBusinessStatusArgs(
    string? Name,
    decimal Percentage,
    string? Color,
    bool IsActive);
