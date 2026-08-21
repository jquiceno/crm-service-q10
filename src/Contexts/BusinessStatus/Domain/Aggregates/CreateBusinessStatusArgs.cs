namespace BusinessStatus.Domain.Aggregates;

/// <summary>
/// Primitive arguments for a new business status. <paramref name="Percentage"/> stays decimal so the
/// aggregate can reject a non-integer value with a domain error of its own instead of leaving it to
/// model binding.
/// </summary>
public sealed record CreateBusinessStatusArgs(
    string? Name,
    decimal Percentage,
    string? Color,
    bool IsActive);
