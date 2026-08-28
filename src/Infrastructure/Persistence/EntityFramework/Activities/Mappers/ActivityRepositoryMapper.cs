using Activities.Domain.Aggregates;
using Activities.Domain.ValueObjects;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;

namespace Infrastructure.Persistence.EntityFramework.Activities.Mappers;

/// <summary>
/// Translates between the persistence entity (raw legacy columns) and the aggregate, keeping EF
/// unaware of the domain. Seeing the whole row is what makes the hard conversions natural: the
/// scope-dependent <c>negact_resultado</c> char resolves against the row's own type, and the
/// (<c>negact_completada</c>, <c>negact_anulada</c>) bit pair collapses into one status
/// (NULL ⇒ false — DEC-6). <see cref="ToDomain"/> fails loudly on values the service does not
/// recognize (D20): with 378 databases drifting apart, silently classifying dirty data would
/// hide real drift.
/// </summary>
internal static class ActivityRepositoryMapper
{
    public static ActivityAggregate ToDomain(Activity entity)
    {
        var type = LegacyActivityCodes.ToType(entity.Type);

        return ActivityAggregate.Reconstruct(
            entity.Id,
            entity.DealId,
            entity.OpportunityId,
            type,
            LegacyActivityCodes.ToStatus(entity.IsCompleted, entity.IsCancelled),
            entity.Title is null ? null : Description.Reconstruct(entity.Title),
            entity.DueAt,
            entity.OutcomeText is null ? null : Outcome.Reconstruct(entity.OutcomeText),
            LegacyActivityCodes.ToOutcomeType(type, entity.OutcomeCode),
            entity.AdvisorId is null ? null : PersonCode.Reconstruct(entity.AdvisorId),
            PersonCode.Reconstruct(entity.CreatedById),
            entity.CreatedAt,
            entity.CompletedAt);
    }

    /// <summary>
    /// Builds the row for an INSERT. Update flows must never overwrite a tracked entity with
    /// this output blindly: the domain does not carry stray outcome codes (types without a
    /// catalogue) nor historic NULL bits, so a blanket copy would normalize legacy data that
    /// DEC-6 forbids touching — the repository (F2.4) must copy changed columns selectively.
    /// </summary>
    public static Activity ToDocument(ActivityAggregate aggregate)
    {
        var (isCompleted, isCancelled) = LegacyActivityCodes.ToStatusBits(aggregate.Status);

        return new Activity
        {
            DealId = aggregate.DealId,
            OpportunityId = aggregate.OpportunityId,
            Type = LegacyActivityCodes.ToTypeCode(aggregate.Type),
            Title = aggregate.Description?.Value,
            OutcomeText = aggregate.Outcome?.Value,
            OutcomeCode = LegacyActivityCodes.ToOutcomeTypeCode(aggregate.OutcomeType),
            CreatedAt = aggregate.CreatedAt ?? throw new InvalidOperationException(
                "Activity.CreatedAt is unset: only factory-created aggregates can be persisted."),
            DueAt = aggregate.DueAt,
            IsCompleted = isCompleted,
            IsCancelled = isCancelled,
            CompletedAt = aggregate.CompletedAt,
            AdvisorId = aggregate.AdvisorId?.Value,
            CreatedById = aggregate.CreatedById.Value,
        };
    }
}
