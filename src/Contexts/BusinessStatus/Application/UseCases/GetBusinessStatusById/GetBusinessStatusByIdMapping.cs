using BusinessStatus.Domain.Aggregates;

namespace BusinessStatus.Application.UseCases.GetBusinessStatusById;

public static class GetBusinessStatusByIdMapping
{
    public static GetBusinessStatusByIdOutputDto ToOutputDto(this BusinessStatusAggregate aggregate) =>
        new(aggregate.Id,
            aggregate.Name,
            aggregate.Percentage,
            aggregate.Color?.Value,
            aggregate.IsActive);
}
