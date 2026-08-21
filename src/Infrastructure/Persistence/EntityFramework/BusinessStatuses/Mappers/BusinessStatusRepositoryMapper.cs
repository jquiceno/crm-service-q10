using BusinessStatus.Domain.Aggregates;

namespace Infrastructure.Persistence.EntityFramework.BusinessStatuses.Mappers;

public static class BusinessStatusRepositoryMapper
{
    public static BusinessStatusAggregate ToDomain(Entities.BusinessStatus row) =>
        BusinessStatusAggregate.Reconstruct(
            row.Id,
            row.Name ?? string.Empty,
            ToWholePercentage(row.Percentage),
            row.Color,
            row.IsActive ?? false);

    public static Entities.BusinessStatus ToDocument(BusinessStatusAggregate aggregate) =>
        new()
        {
            Name = aggregate.Name,
            IsActive = aggregate.IsActive,
            Percentage = aggregate.Percentage,
            Color = aggregate.Color?.Value
        };

    private static int? ToWholePercentage(decimal? persisted) =>
        persisted.HasValue && decimal.Truncate(persisted.Value) == persisted.Value
            ? (int)persisted.Value
            : null;
}
