namespace Infrastructure.Persistence.EntityFramework.LossReasons.Entities;

/// <summary>
/// Keyless read-only projection of tbl_opo_negocios used solely to check
/// whether a LossReason is assigned to at least one deal before deletion.
/// No repository is created for this entity: it is a foreign table, not an
/// Aggregate of this context.
/// </summary>
public sealed class DealLossReasonUsage
{
    public int? NegCauConsecutivo { get; set; }
}
