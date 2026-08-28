namespace Activities.Domain.Filters;

/// <summary>Search criteria for <c>IActivityRepository.SearchAsync</c>.</summary>
/// <remarks><see cref="DealStateId"/> is <c>int</c>, not <c>string</c>: matches the real column type.</remarks>
public sealed record ActivityFilter(int? DealId, int? OpportunityId, int? DealStateId);
