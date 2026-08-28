namespace Activities.Domain.Filters;

/// <summary>
/// Search criteria for <c>IActivityRepository.SearchAsync</c>. Requiring at least one field is the
/// application layer's job (the request validator), not this record's — a filter with every field
/// null is a legal, if unusual, value here.
/// </summary>
/// <remarks>
/// <see cref="DealStateId"/> is <c>int?</c>, not the <c>string?</c> the original task write-up
/// assumed: Tarea 6 confirmed <c>tbl_opo_negocios.neg_negest_consecutivo</c> (the reader's
/// <c>Deal.DealStateId</c>) is <c>int NOT NULL</c>, so the filter matches the real column type.
/// </remarks>
public sealed record ActivityFilter(int? DealId, int? OpportunityId, int? DealStateId);
