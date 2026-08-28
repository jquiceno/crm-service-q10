namespace Activities.Domain.Models;

/// <summary>
/// What the Activities context needs to know about a deal before writing an activity against it.
/// </summary>
/// <remarks>
/// A read model, not an aggregate: no identity, no rules. The deal and its opportunity belong to
/// another context; this record only carries the three facts the create flow validates.
/// <para>
/// <see cref="OpportunityId"/> is derived from the deal and never accepted as input (DEC-1). It is
/// nullable only to express "no deal was found" — the legacy column
/// <c>tbl_opo_negocios.neg_opo_consecutivo</c> is <c>NOT NULL</c> [verified in DB], so an existing
/// deal always carries one.
/// </para>
/// </remarks>
public sealed record DealContext(bool Exists, int? OpportunityId, bool OpportunityArchived)
{
    /// <summary>No deal matched the requested id.</summary>
    public static readonly DealContext NotFound = new(Exists: false, OpportunityId: null, OpportunityArchived: false);
}
