namespace Infrastructure.Persistence.EntityFramework.BusinessStatuses;

/// <summary>
/// What the L2 cache stores for a page of the catalogue. Flat records with public constructors, so
/// <c>System.Text.Json</c> (de)serializes them without configuration — the aggregate itself can
/// never be cached: its constructor is private, so every hit would throw and degrade silently to a
/// 0 % hit rate.
/// </summary>
public sealed record BusinessStatusListSnapshot(
    IReadOnlyList<BusinessStatusSnapshotItem> Items,
    int TotalCount);

public sealed record BusinessStatusSnapshotItem(
    int Id,
    string Name,
    int? Percentage,
    string? Color,
    bool IsActive);
