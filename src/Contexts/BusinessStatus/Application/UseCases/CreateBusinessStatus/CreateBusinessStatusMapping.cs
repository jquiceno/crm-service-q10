using BusinessStatus.Domain.Aggregates;
using Shared.Results;

namespace BusinessStatus.Application.UseCases.CreateBusinessStatus;

public static class CreateBusinessStatusMapping
{
    public static Result<BusinessStatusAggregate> ToAggregate(this CreateBusinessStatusInputDto input) =>
        BusinessStatusAggregate.Create(
            new CreateBusinessStatusArgs(input.Name, input.Percentage, input.Color, input.IsActive));

    public static CreateBusinessStatusOutputDto ToOutputDto(this BusinessStatusAggregate aggregate) =>
        new(aggregate.Id,
            aggregate.Name,
            aggregate.Percentage,
            aggregate.Color?.Value,
            aggregate.IsActive);
}
