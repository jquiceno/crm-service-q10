using LossReason.Domain.Aggregates;

namespace Infrastructure.Persistence.EntityFramework.LossReasons.Mappers;

public static class LossReasonRepositoryMapper
{
    public static LossReasonAggregate ToDomain(Entities.LossReason document) =>
        LossReasonAggregate.Reconstruct(
            document.Id,
            document.Name ?? string.Empty,
            document.IsActive ?? false);

    // The identity column is deliberately left unset: the database assigns it on insert.
    public static Entities.LossReason ToDocument(LossReasonAggregate aggregate) =>
        new()
        {
            Name = aggregate.Name,
            IsActive = aggregate.IsActive
        };
}
