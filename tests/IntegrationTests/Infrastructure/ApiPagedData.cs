namespace IntegrationTests.Infrastructure;

/// <summary>
/// Mirrors the paged payload shape returned by <c>HttpOkPagedResult</c>.
/// </summary>
public sealed record ApiPagedData<T>(IReadOnlyList<T> Items, int TotalCount);
