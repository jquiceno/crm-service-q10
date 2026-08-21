using BusinessStatus.Domain.Aggregates;

namespace BusinessStatus.Application.UseCases.UpdateBusinessStatus;

public static class UpdateBusinessStatusMapping
{
    public static UpdateBusinessStatusArgs ToUpdateArgs(this UpdateBusinessStatusInputDto input) =>
        new(input.Name, input.Percentage, input.Color, input.IsActive);

    public static UpdateBusinessStatusOutputDto ToOutputDto(this BusinessStatusAggregate aggregate) =>
        new(aggregate.Id,
            aggregate.Name,
            aggregate.Percentage,
            aggregate.Color?.Value,
            aggregate.IsActive);
}
