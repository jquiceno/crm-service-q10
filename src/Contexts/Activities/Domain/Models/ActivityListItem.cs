using Activities.Domain.Aggregates;

namespace Activities.Domain.Models;

/// <summary>
/// One row of the activities listing: the aggregate plus the display names the public contract
/// carries (§6.1).
/// </summary>
/// <remarks>
/// A read model, not an aggregate: the names live in the deal, opportunity and person tables of
/// the institution, which this context reads but never owns nor writes, so they travel beside the
/// aggregate instead of inside it.
/// <para>
/// Every name is nullable because the legacy columns are: an activity whose advisor is missing
/// (migrated history, §4.1) has no person row to name.
/// </para>
/// <para>
/// <see cref="CreatedByName"/> is nullable for a different reason: <c>CreatedById</c> itself is
/// never missing (every factory requires it), but the <c>Person</c> row it points to might no
/// longer exist.
/// </para>
/// </remarks>
public sealed record ActivityListItem(
    ActivityAggregate Activity,
    string? DealName,
    string? OpportunityName,
    string? AdvisorName,
    string? AdvisorIdentification,
    string? CreatedByName);
