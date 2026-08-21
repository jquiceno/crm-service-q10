using BusinessStatus.Domain.Aggregates;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses.Entities;

namespace Infrastructure.Persistence.EntityFramework.BusinessStatuses.Mappers;

public static class BusinessStatusRepositoryMapper
{
    public static BusinessStatusAggregate ToDomain(BusinessStatusRow row) =>
        BusinessStatusAggregate.Reconstruct(
            row.Id,
            row.Name ?? string.Empty,
            ToWholePercentage(row.Percentage),
            row.Color,
            row.IsActive ?? false);

    public static BusinessStatusRow ToDocument(BusinessStatusAggregate aggregate) =>
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
